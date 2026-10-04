using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Fookbase.Api.Modules.Admin.DTOs.Responses;
using Fookbase.Api.Modules.Admin.Services;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class AdminDashboardTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    [Theory]
    [InlineData("{}", HttpStatusCode.BadRequest, true, true)]
    [InlineData("{\"isActive\":null}", HttpStatusCode.BadRequest, true, true)]
    [InlineData("{\"isActive\":false}", HttpStatusCode.OK, true, false)]
    [InlineData("{\"isActive\":true}", HttpStatusCode.OK, false, true)]
    public async Task User_status_requires_an_explicit_boolean_before_changing_the_account(
        string body, HttpStatusCode expectedStatus, bool initialIsActive, bool expectedIsActive)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var adminEmail = $"status-admin-{suffix}@example.test";
        const string password = "Dashboard-test123!";
        using var client = factory.CreateClient();
        var administrator = await TestAccountSetup.CreateAsync(
            factory, client, adminEmail, $"status-admin-{suffix}", password);
        var target = await TestAccountSetup.CreateAsync(
            factory, client, $"status-user-{suffix}@example.test", $"status-user-{suffix}", password);
        using (var scope = factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roleManager.RoleExistsAsync("Admin"))
                Assert.True((await roleManager.CreateAsync(new IdentityRole<Guid>("Admin") { Id = Guid.NewGuid() })).Succeeded);

            var manager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var admin = await manager.FindByIdAsync(administrator.User.Id.ToString());
            Assert.NotNull(admin);
            Assert.True((await manager.AddToRoleAsync(admin, "Admin")).Succeeded);
            if (!initialIsActive)
            {
                var user = await manager.FindByIdAsync(target.User.Id.ToString());
                Assert.NotNull(user);
                user.Disable();
                Assert.True((await manager.UpdateAsync(user)).Succeeded);
            }
        }

        using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(adminEmail, password));
        login.EnsureSuccessStatusCode();
        var session = await login.Content.ReadApiDataAsync<AuthenticationResponse>();
        Assert.NotNull(session);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await client.PatchAsync($"/api/admin/users/{target.User.Id}/status", content);

        Assert.Equal(expectedStatus, response.StatusCode);
        if (expectedStatus == HttpStatusCode.BadRequest)
        {
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Contains(problem.RootElement.GetProperty("errors").EnumerateObject(), field =>
                field.Name.EndsWith(nameof(UpdateUserStatusRequest.IsActive), StringComparison.Ordinal) &&
                field.Value.EnumerateArray().Any(error => error.GetString() == "Trạng thái hoạt động là bắt buộc."));
        }

        using var verificationScope = factory.Services.CreateScope();
        var persisted = await verificationScope.ServiceProvider.GetRequiredService<UserManager<User>>()
            .FindByIdAsync(target.User.Id.ToString());
        Assert.NotNull(persisted);
        Assert.Equal(expectedIsActive, persisted.IsActive);
    }

    [Fact]
    public async Task Dashboard_requires_authentication()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/dashboard")).StatusCode);
    }

    [Theory]
    [InlineData("registration")]
    [InlineData("login")]
    [InlineData("current-user")]
    [InlineData("refresh")]
    public async Task Configured_bootstrap_email_does_not_grant_administrator_access(string operation)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var email = $"bootstrap-{suffix}@example.test";
        const string password = "Dashboard-test123!";
        await using var ordinaryApp = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Admin:BootstrapEmail", ""));
        await using var configuredApp = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Admin:BootstrapEmail", email));
        using var ordinaryClient = ordinaryApp.CreateClient();
        using var client = configuredApp.CreateClient();
        AuthenticationResponse session;
        if (operation == "registration")
        {
            using var start = await client.PostAsJsonAsync("/api/auth/registration/start",
                new RegistrationStartRequest("Bootstrap", suffix, new DateOnly(2000, 1, 2), "other", email, password));
            Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
            var challenge = await start.Content.ReadApiDataAsync<RegistrationChallengeResponse>();
            Assert.NotNull(challenge);
            var code = configuredApp.Services.GetRequiredService<TestContactOtpSender>().LastCodeFor(email);
            using var registration = await client.PostAsJsonAsync("/api/auth/registration/verify",
                new RegistrationVerifyRequest(challenge.ChallengeId, code));
            Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
            session = await registration.Content.ReadApiDataAsync<AuthenticationResponse>();
            Assert.NotNull(session);
        }
        else
        {
            session = await TestAccountSetup.CreateAsync(
                ordinaryApp, ordinaryClient, email, $"bootstrap-{suffix}", password);
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        if (operation == "login" || operation == "refresh")
        {
            using var response = operation == "login"
                ? await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password))
                : await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(session.RefreshToken));
            response.EnsureSuccessStatusCode();
            session = await response.Content.ReadApiDataAsync<AuthenticationResponse>();
            Assert.NotNull(session);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        }

        if (operation == "current-user")
        {
            using var response = await client.GetAsync("/api/auth/me");
            response.EnsureSuccessStatusCode();
            var currentUser = await response.Content.ReadApiDataAsync<AuthenticatedUserResponse>();
            Assert.NotNull(currentUser);
            Assert.DoesNotContain("Admin", currentUser.Roles);
        }

        Assert.DoesNotContain("Admin", session.User.Roles);
        using var scope = configuredApp.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.False(await userManager.IsInRoleAsync(user, "Admin"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/dashboard")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dashboard_only_returns_analytics_to_administrators(bool isAdmin)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var email = $"dashboard-{suffix}@example.test";
        using var client = factory.CreateClient();
        await TestAccountSetup.CreateAsync(
            factory, client, email, $"dashboard-{suffix}", "Dashboard-test123!");
        if (isAdmin)
        {
            using var scope = factory.Services.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                var createRole = await roleManager.CreateAsync(new IdentityRole<Guid>("Admin") { Id = Guid.NewGuid() });
                Assert.True(createRole.Succeeded);
            }

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);
            Assert.True((await userManager.AddToRoleAsync(user, "Admin")).Succeeded);
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Dashboard-test123!"));
        login.EnsureSuccessStatusCode();
        var session = await login.Content.ReadApiDataAsync<AuthenticationResponse>();
        Assert.NotNull(session);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var response = await client.GetAsync("/api/admin/dashboard");

        Assert.Equal(isAdmin ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
        if (isAdmin)
        {
            var dashboard = await response.Content.ReadFromJsonAsync<AdminDashboardResponse>();
            Assert.NotNull(dashboard);
            Assert.Equal(30, dashboard.Activity.Count);
            Assert.Equal(4, dashboard.ReportStatuses.Count);
            Assert.Equal(dashboard.PendingReports,
                dashboard.ReportStatuses.Single(item => item.Status == "pending").Count);
        }
    }

    [Fact]
    public async Task Dashboard_aggregates_utc_days_and_statuses_without_deleted_posts()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var service = scope.ServiceProvider.GetRequiredService<AdministrationUseCase>();
        var before = await service.GetDashboardAsync();
        var today = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var firstDay = today.AddDays(-29);
        var user = new User(Guid.NewGuid(), "dashboard@example.test", "dashboard-test", firstDay);
        db.Users.Add(user);
        db.Users.Add(new User(Guid.NewGuid(), "old-dashboard@example.test", "old-dashboard", firstDay.AddTicks(-1)));
        var post = new Post(Guid.NewGuid(), user.Id, "Visible", PostPrivacy.PUBLIC, today);
        var deleted = new Post(Guid.NewGuid(), user.Id, "Deleted", PostPrivacy.PUBLIC, today);
        deleted.Delete(today);
        db.Posts.AddRange(post, deleted);
        foreach (var status in Enum.GetValues<ContentReportStatus>())
        {
            var report = new ContentReport(Guid.NewGuid(), ReportTargetType.POST, post.Id,
                ReportReason.SPAM, null, today.AddTicks(-1));
            if (status != ContentReportStatus.PENDING) report.UpdateStatus(status, today);
            db.ContentReports.Add(report);
        }
        await db.SaveChangesAsync();

        var result = await service.GetDashboardAsync();

        Assert.Equal(30, result.Activity.Count);
        Assert.Equal(DateOnly.FromDateTime(firstDay.UtcDateTime), result.Activity[0].Date);
        Assert.Equal(DateOnly.FromDateTime(today.UtcDateTime), result.Activity[^1].Date);
        Assert.Equal(before.TotalUsers + 2, result.TotalUsers);
        Assert.Equal(before.ActiveUsers + 2, result.ActiveUsers);
        Assert.Equal(before.ActivePosts + 1, result.ActivePosts);
        Assert.Equal(before.PendingReports + 1, result.PendingReports);
        for (var index = 0; index < 30; index++)
        {
            Assert.Equal(before.Activity[index].NewUsers + (index == 0 ? 1 : 0), result.Activity[index].NewUsers);
            Assert.Equal(before.Activity[index].NewPosts + (index == 29 ? 1 : 0), result.Activity[index].NewPosts);
            Assert.Equal(before.Activity[index].NewReports + (index == 28 ? 4 : 0), result.Activity[index].NewReports);
        }
        Assert.Equal(4, result.ReportStatuses.Count);
        foreach (var status in result.ReportStatuses)
            Assert.Equal(before.ReportStatuses.Single(item => item.Status == status.Status).Count + 1, status.Count);
    }
}
