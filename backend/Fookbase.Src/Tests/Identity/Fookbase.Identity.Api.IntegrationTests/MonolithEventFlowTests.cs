using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using System.Net;
using System.Net.Http.Json;
using Fookbase.Api.Modules.Friends.Data;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Identity.Data;
using Fookbase.Api.Modules.Media.Data;
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
        Assert.True(await scope.ServiceProvider.GetRequiredService<FookbaseDbContext>()
            .UserProfiles.AnyAsync(item => item.UserId == authentication.User.Id));
    }
}

public sealed class MonolithApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__FookbaseDatabase")
            ?? throw new InvalidOperationException("Fookbase development database connection string is required.");
        builder.UseEnvironment("Testing");
        builder.UseSetting("Minio:AccessKey", "integration-tests");
        builder.UseSetting("Minio:SecretKey", "integration-tests");
        builder.UseSetting("Minio:BucketInitializationEnabled", "false");
        builder.UseSetting("Cloudinary:CloudName", "integration-tests");
        builder.UseSetting("Cloudinary:ApiKey", "test-api-key");
        builder.UseSetting("Cloudinary:ApiSecret", "test-api-secret");
        builder.UseSetting("Media:CleanupIntervalSeconds", "3600");
        builder.UseSetting("Jwt:SigningKey", "identity-integration-tests-signing-key-with-32-characters");
        builder.UseSetting("ConnectionStrings:FookbaseDatabase", connectionString);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<Fookbase.Api.Persistence.FookbaseDbContext>()
            .Database.Migrate();

        return host;
    }
}
