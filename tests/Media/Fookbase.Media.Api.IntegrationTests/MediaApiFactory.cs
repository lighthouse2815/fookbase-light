using Fookbase.Media.Application.Abstractions;
using Fookbase.Media.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Fookbase.Media.Api.IntegrationTests;

public sealed class MediaApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var mediaConnectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__MediaDatabase");
        if (string.IsNullOrWhiteSpace(mediaConnectionString))
        {
            var postsConnectionString = Environment.GetEnvironmentVariable(
                "ConnectionStrings__PostsDatabase")
                ?? throw new InvalidOperationException(
                    "A Media or Posts development database connection string is required.");
            mediaConnectionString = postsConnectionString.Replace(
                "Database=posts_db",
                "Database=media_db",
                StringComparison.OrdinalIgnoreCase);
        }

        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:MediaDatabase", mediaConnectionString);
        builder.UseSetting("Minio:AccessKey", "integration-tests");
        builder.UseSetting("Minio:SecretKey", "integration-tests");
        builder.UseSetting("Minio:BucketInitializationEnabled", "false");
        builder.UseSetting("RabbitMq:UserName", "integration-tests");
        builder.UseSetting("RabbitMq:Password", "integration-tests");
        builder.UseSetting("RabbitMq:ConsumerEnabled", "false");
        builder.UseSetting("Outbox:PublisherEnabled", "false");
        builder.UseSetting("InternalServices:Token", "integration-tests-internal-token-32-chars");
        builder.UseSetting("Media:CleanupIntervalSeconds", "3600");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IObjectStorage>();
            services.AddSingleton<InMemoryObjectStorage>();
            services.AddSingleton<IObjectStorage>(provider =>
                provider.GetRequiredService<InMemoryObjectStorage>());
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        dbContext.Database.Migrate();

        return host;
    }
}
