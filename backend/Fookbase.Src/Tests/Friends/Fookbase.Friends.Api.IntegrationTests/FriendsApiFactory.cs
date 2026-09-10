using Fookbase.Api.Modules.Friends.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Fookbase.Friends.Api.IntegrationTests;

public sealed class FriendsApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__FriendsDatabase")
            ?? throw new InvalidOperationException("Friends development database connection string is required.");
        builder.UseEnvironment("Testing");
        ConfigureModuleConnections(builder, connectionString);
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-must-have-32-characters");
        builder.UseSetting("Minio:AccessKey", "integration-tests");
        builder.UseSetting("Minio:SecretKey", "integration-tests");
        builder.UseSetting("Minio:BucketInitializationEnabled", "false");
        builder.UseSetting("Media:CleanupIntervalSeconds", "3600");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FriendsDbContext>();
        dbContext.Database.Migrate();

        return host;
    }

    private static void ConfigureModuleConnections(IWebHostBuilder builder, string connectionString)
    {
        foreach (var module in new[] { "Identity", "Users", "Friends", "Messages", "Posts", "Media" })
        {
            builder.UseSetting($"ConnectionStrings:{module}Database", connectionString);
        }
    }
}
