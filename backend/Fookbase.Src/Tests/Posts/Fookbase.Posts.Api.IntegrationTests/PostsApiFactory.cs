using Fookbase.Api.Modules.Posts.Repositories;
using Fookbase.Api.Modules.Posts.Services.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class PostsApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PostsDatabase")
            ?? throw new InvalidOperationException("Posts development database connection string is required.");
        builder.UseEnvironment("Testing");
        ConfigureModuleConnections(builder, connectionString);
        builder.UseSetting("Outbox:PublisherEnabled", "false");
        builder.UseSetting("Minio:AccessKey", "integration-tests");
        builder.UseSetting("Minio:SecretKey", "integration-tests");
        builder.UseSetting("Minio:BucketInitializationEnabled", "false");
        builder.UseSetting("Media:CleanupIntervalSeconds", "3600");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMediaReadUrlClient>();
            services.AddSingleton<IMediaReadUrlClient, FakeMediaReadUrlClient>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostsDbContext>();
        dbContext.Database.Migrate();

        return host;
    }

    private static void ConfigureModuleConnections(IWebHostBuilder builder, string connectionString)
    {
        foreach (var module in new[] { "Identity", "Users", "Friends", "Posts", "Media" })
        {
            builder.UseSetting($"ConnectionStrings:{module}Database", connectionString);
        }
    }
}
