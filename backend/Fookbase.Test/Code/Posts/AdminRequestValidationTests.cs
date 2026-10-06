using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Fookbase.Api.Modules.Admin.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class AdminRequestValidationTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Fact]
    public async Task Invalid_reason_or_note_is_rejected_before_moderating_an_account_or_report()
    {
        var (client, moderatorId, userId) = await CreateAdminClientAsync();
        using (client)
        {
            var paths = new[]
            {
                $"users/{userId}/warn", $"users/{userId}/suspend", $"users/{userId}/unsuspend",
                $"users/{userId}/disable", $"users/{userId}/enable",
                $"reports/{Guid.NewGuid()}/dismiss", $"reports/{Guid.NewGuid()}/remove-content",
                $"reports/{Guid.NewGuid()}/warn-user", $"reports/{Guid.NewGuid()}/suspend-user"
            };
            foreach (var path in paths)
            {
                foreach (var reason in new[] { "", "   ", new string('a', 501) })
                {
                    using var response = await client.PostAsJsonAsync("/api/admin/" + path,
                        new { reason, durationHours = 24 });
                    await AssertValidationAsync(response, "Reason");
                }

                using var invalidNote = await client.PostAsJsonAsync("/api/admin/" + path,
                    new { durationHours = 24, internalNote = new string('a', 2_001) });
                await AssertValidationAsync(invalidNote, "InternalNote");
            }
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.False(await db.UserModerationStates.AnyAsync(state => state.UserId == userId));
        Assert.False(await db.ModerationActions.AnyAsync(action => action.ModeratorUserId == moderatorId));
    }

    [Theory]
    [InlineData("/api/admin/users?offset=-1", "Offset")]
    [InlineData("/api/admin/users?limit=0", "Limit")]
    [InlineData("/api/admin/users?limit=101", "Limit")]
    [InlineData("/api/admin/reports?offset=-1", "Offset")]
    [InlineData("/api/admin/reports?limit=0", "Limit")]
    [InlineData("/api/admin/reports?limit=101", "Limit")]
    [InlineData("/api/admin/reports?status=unknown", "Status")]
    [InlineData("/api/admin/reports?status=42", "Status")]
    [InlineData("/api/admin/reports?targetType=unknown", "TargetType")]
    [InlineData("/api/admin/reports?targetType=42", "TargetType")]
    [InlineData("/api/admin/reports?cursor=invalid", "Cursor")]
    [InlineData("/api/admin/users/{id}/moderation-history?limit=0", "Limit")]
    [InlineData("/api/admin/users/{id}/moderation-history?limit=101", "Limit")]
    [InlineData("/api/admin/users/{id}/moderation-history?cursor=invalid", "Cursor")]
    public async Task Invalid_page_input_returns_field_validation_errors(string path, string field)
    {
        var (client, _, userId) = await CreateAdminClientAsync();
        using (client)
        using (var response = await client.GetAsync(path.Replace("{id}", userId.ToString())))
        {
            await AssertValidationAsync(response, field);
        }
    }

    [Fact]
    public async Task Cursor_with_out_of_range_ticks_returns_validation_error()
    {
        var (client, _, userId) = await CreateAdminClientAsync();
        using (client)
        {
            var cursor = Uri.EscapeDataString(Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"3155378976000000000:{Guid.NewGuid():N}")));
            foreach (var path in new[] { "/api/admin/reports", $"/api/admin/users/{userId}/moderation-history" })
            {
                using var response = await client.GetAsync(path + "?cursor=" + cursor);
                await AssertValidationAsync(response, "Cursor");
            }
        }
    }

    [Fact]
    public async Task Suspension_requires_a_valid_duration_or_future_expiry_before_changing_state()
    {
        var (client, moderatorId, userId) = await CreateAdminClientAsync();
        using (client)
        {
            var bodies = new object[]
            {
                new { }, new { durationHours = 0 }, new { durationHours = -1 }, new { durationHours = 8_761 },
                new { suspendedUntilUtc = DateTimeOffset.UtcNow.AddHours(-1) },
                new { suspendedUntilUtc = DateTimeOffset.UtcNow.AddDays(366) }
            };
            for (var index = 0; index < bodies.Length; index++)
            {
                using var response = await client.PostAsJsonAsync($"/api/admin/users/{userId}/suspend", bodies[index]);
                await AssertValidationAsync(response, index < 4 ? "DurationHours" : "SuspendedUntilUtc");
            }
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.False(await db.UserModerationStates.AnyAsync(state => state.UserId == userId));
        Assert.False(await db.ModerationActions.AnyAsync(action => action.ModeratorUserId == moderatorId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("pending")]
    [InlineData("PENDING")]
    [InlineData("0")]
    [InlineData("42")]
    [InlineData("unknown")]
    public async Task Report_status_must_be_a_supported_non_pending_value(string? status)
    {
        var (client, _, _) = await CreateAdminClientAsync();
        using (client)
        using (var response = await client.PatchAsJsonAsync($"/api/admin/reports/{Guid.NewGuid()}/status", new { status }))
        {
            await AssertValidationAsync(response, "Status");
        }
    }

    [Theory]
    [InlineData("reviewed", ContentReportStatus.REVIEWED)]
    [InlineData("ReSoLvEd", ContentReportStatus.RESOLVED)]
    [InlineData("DISMISSED", ContentReportStatus.DISMISSED)]
    [InlineData("1", ContentReportStatus.REVIEWED)]
    [InlineData("2", ContentReportStatus.RESOLVED)]
    [InlineData("3", ContentReportStatus.DISMISSED)]
    public async Task Report_status_updates_preserve_supported_enum_formats(string status, ContentReportStatus expected)
    {
        var (client, moderatorId, userId) = await CreateAdminClientAsync();
        using (client)
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            var report = new ContentReport(moderatorId, ReportTargetType.USER, userId,
                ReportReason.SPAM, null, DateTimeOffset.UtcNow);
            db.ContentReports.Add(report);
            await db.SaveChangesAsync();

            using var response = await client.PatchAsJsonAsync($"/api/admin/reports/{report.Id}/status", new { status });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(expected.ToString().ToLowerInvariant(), document.RootElement.GetProperty("status").GetString());
            var updated = await db.ContentReports.AsNoTracking().SingleAsync(item => item.Id == report.Id);
            Assert.Equal(expected, updated.Status);
            Assert.NotNull(updated.ResolvedAtUtc);
        }
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(" ", " ")]
    [InlineData("pEnDiNg", "UsEr")]
    [InlineData("0", "0")]
    public async Task Report_filters_preserve_optional_values_and_supported_enum_formats(string? status, string? targetType)
    {
        var (client, _, userId) = await CreateAdminClientAsync();
        using (client)
        {
            var path = "/api/admin/reports";
            if (status is not null || targetType is not null)
            {
                path += "?status=" + Uri.EscapeDataString(status ?? "") +
                    "&targetType=" + Uri.EscapeDataString(targetType ?? "") + "&cursor=%20";
            }
            using var reports = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, reports.StatusCode);
            using var history = await client.GetAsync($"/api/admin/users/{userId}/moderation-history?cursor=%20");
            Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        }
    }

    [Fact]
    public async Task Optional_reasons_and_trimmed_length_boundaries_keep_existing_behavior()
    {
        var (client, _, userId) = await CreateAdminClientAsync();
        using (client)
        {
            using var defaultReason = await client.PostAsJsonAsync($"/api/admin/users/{userId}/warn", new { });
            Assert.Equal(HttpStatusCode.OK, defaultReason.StatusCode);
            Assert.Equal("Account warning.", (await defaultReason.Content.ReadFromJsonAsync<ModerationActionResponse>())!.Reason);
            using var trimmed = await client.PostAsJsonAsync($"/api/admin/users/{userId}/warn",
                new { reason = " " + new string('a', 500) + " ", internalNote = " " + new string('b', 2_000) + " " });
            Assert.Equal(HttpStatusCode.OK, trimmed.StatusCode);
            var action = await trimmed.Content.ReadFromJsonAsync<ModerationActionResponse>();
            Assert.Equal(new string('a', 500), action!.Reason);
            Assert.Equal(new string('b', 2_000), action.InternalNote);
            var until = DateTimeOffset.UtcNow.AddHours(24);
            using var suspended = await client.PostAsJsonAsync($"/api/admin/users/{userId}/suspend",
                new { durationHours = -1, suspendedUntilUtc = until });
            Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
            Assert.Equal(until, (await suspended.Content.ReadFromJsonAsync<ModerationActionResponse>())!.ExpiresAtUtc);
        }
    }

    [Fact]
    public async Task Queue_and_history_cursors_keep_pagination_order_and_response_shapes()
    {
        var (client, moderatorId, userId) = await CreateAdminClientAsync();
        using (client)
        {
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
                db.ContentReports.AddRange(
                    new ContentReport(moderatorId, ReportTargetType.USER, userId, ReportReason.SPAM, null, DateTimeOffset.UtcNow.AddMinutes(-2)),
                    new ContentReport(userId, ReportTargetType.USER, moderatorId, ReportReason.SPAM, null, DateTimeOffset.UtcNow.AddMinutes(-1)));
                await db.SaveChangesAsync();
            }
            var firstQueue = await client.GetFromJsonAsync<ModerationQueuePageResponse>("/api/admin/reports?targetType=USER&status=PENDING&limit=1");
            Assert.Single(firstQueue!.Items);
            Assert.NotNull(firstQueue.NextCursor);
            var nextQueue = await client.GetFromJsonAsync<ModerationQueuePageResponse>(
                "/api/admin/reports?targetType=user&status=pending&limit=1&cursor=" + Uri.EscapeDataString(firstQueue.NextCursor));
            Assert.Single(nextQueue!.Items);
            Assert.True(firstQueue.Items[0].CreatedAtUtc <= nextQueue.Items[0].CreatedAtUtc);
            Assert.NotEqual(firstQueue.Items[0].Id, nextQueue.Items[0].Id);

            for (var index = 0; index < 2; index++)
            {
                using var warned = await client.PostAsJsonAsync($"/api/admin/users/{userId}/warn", new { });
                Assert.Equal(HttpStatusCode.OK, warned.StatusCode);
            }
            var firstHistory = await client.GetFromJsonAsync<ModerationActionPageResponse>($"/api/admin/users/{userId}/moderation-history?limit=1");
            Assert.Single(firstHistory!.Items);
            Assert.NotNull(firstHistory.NextCursor);
            var nextHistory = await client.GetFromJsonAsync<ModerationActionPageResponse>(
                $"/api/admin/users/{userId}/moderation-history?limit=1&cursor=" + Uri.EscapeDataString(firstHistory.NextCursor));
            Assert.Single(nextHistory!.Items);
            Assert.True(firstHistory.Items[0].CreatedAtUtc >= nextHistory.Items[0].CreatedAtUtc);
            Assert.NotEqual(firstHistory.Items[0].Id, nextHistory.Items[0].Id);
            Assert.Null(nextHistory.NextCursor);
        }
    }

    private async Task<(HttpClient Client, Guid ModeratorId, Guid UserId)> CreateAdminClientAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var moderatorId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.Users.AddRange(new User(moderatorId, $"admin-{moderatorId:N}@example.test", $"admin_{moderatorId:N}"[..32], DateTimeOffset.UtcNow),
            new User(userId, $"user-{userId:N}@example.test", $"user_{userId:N}"[..32], DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var token = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"],
            [new Claim(JwtRegisteredClaimNames.Sub, moderatorId.ToString()), new Claim(ClaimTypes.Role, "Admin")],
            notBefore: DateTime.UtcNow.AddSeconds(-1), expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return (client, moderatorId, userId);
    }

    private static async Task AssertValidationAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validation_failed", document.RootElement.GetProperty("code").GetString());
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty(field, out _));
    }
}
