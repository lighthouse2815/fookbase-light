using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fookbase.Api.Modules.Admin.DTOs.Responses;
using Fookbase.Api.Modules.Admin.Services;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class AdminDashboardTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
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
        using var registration = await (operation == "registration" ? client : ordinaryClient)
            .PostAsJsonAsync("/api/auth/register",
                new RegisterRequest(email, $"bootstrap-{suffix}", password));
        registration.EnsureSuccessStatusCode();
        var session = await registration.Content.ReadApiDataAsync<AuthenticationResponse>();
        Assert.NotNull(session);
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
        var registration = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, $"dashboard-{suffix}", "Dashboard-test123!"));
        registration.EnsureSuccessStatusCode();
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
        var post = Post.Create(Guid.NewGuid(), user.Id, "Visible", PostPrivacy.Public, today);
        var deleted = Post.Create(Guid.NewGuid(), user.Id, "Deleted", PostPrivacy.Public, today);
        deleted.Delete(today);
        db.Posts.AddRange(post, deleted);
        foreach (var status in Enum.GetValues<ContentReportStatus>())
        {
            var report = ContentReport.Create(Guid.NewGuid(), ReportTargetType.Post, post.Id,
                ReportReason.Spam, null, today.AddTicks(-1));
            if (status != ContentReportStatus.Pending) report.UpdateStatus(status, today);
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
