using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using System.Net;
using System.Net.Http.Json;
using Fookbase.Api.Modules.Friends.Data;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Identity.Data;
using Fookbase.Api.Modules.Media.Repositories;
using Fookbase.Api.Modules.Posts.Data;
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
    public async Task Registration_creates_the_user_profile_directly()
    {
        using var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..16];
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest($"mono-{suffix}@example.com", $"mono-{suffix}", "Password123!"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var authentication = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();
        Assert.NotNull(authentication);

        using var scope = factory.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<UsersDbContext>()
            .UserProfiles.AnyAsync(item => item.UserId == authentication.User.Id));
    }
}

public sealed class MonolithApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
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
