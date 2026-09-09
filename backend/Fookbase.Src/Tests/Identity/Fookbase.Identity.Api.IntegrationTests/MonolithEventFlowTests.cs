using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using System.Net;
using System.Net.Http.Json;
using Fookbase.Api.Modules.Friends.Data;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Identity.Data;
using Fookbase.Api.Modules.Media.Repositories;
using Fookbase.Api.Modules.Posts.Repositories;
using Fookbase.Api.Modules.Users.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class MonolithEventFlowTests(MonolithApiFactory factory)
    : IClassFixture<MonolithApiFactory>
{
    [Fact]
    public async Task Registration_is_projected_to_modules_that_require_user_data()
    {
        using var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..16];
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest($"mono-{suffix}@example.com", $"mono-{suffix}", "Password123!"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var authentication = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();
        Assert.NotNull(authentication);

        await WaitUntilAsync(async () =>
        {
            using var scope = factory.Services.CreateScope();
            var userId = authentication.User.Id;
            var identityOutbox = await scope.ServiceProvider
                .GetRequiredService<IdentityDbContext>()
                .OutboxMessages.AsNoTracking().ToListAsync();
            return await scope.ServiceProvider.GetRequiredService<UsersDbContext>()
                       .UserProfiles.AnyAsync(item => item.UserId == userId) &&
                   await scope.ServiceProvider.GetRequiredService<PostsDbContext>()
                       .KnownUsers.AnyAsync(item => item.UserId == userId) &&
                   await scope.ServiceProvider.GetRequiredService<MediaDbContext>()
                       .KnownUsers.AnyAsync(item => item.UserId == userId) &&
                   identityOutbox.Any(item =>
                       item.Payload.Contains(userId.ToString()) && item.ProcessedAtUtc != null);
        });
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(100);
        }

        Assert.Fail("The registration event was not projected to every module in time.");
    }
}

public sealed class MonolithApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Outbox:PublisherEnabled", "true");
        builder.UseSetting("Outbox:PollingIntervalSeconds", "1");
        builder.UseSetting("Minio:AccessKey", "integration-tests");
        builder.UseSetting("Minio:SecretKey", "integration-tests");
        builder.UseSetting("Minio:BucketInitializationEnabled", "false");
        builder.UseSetting("Media:CleanupIntervalSeconds", "3600");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<UsersDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<FriendsDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<PostsDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<MediaDbContext>().Database.Migrate();

        return host;
    }
}
