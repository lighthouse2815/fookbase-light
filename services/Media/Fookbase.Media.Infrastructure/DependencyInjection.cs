using Fookbase.Media.Application.Abstractions;
using Fookbase.Media.Application.Media;
using Fookbase.Media.Infrastructure.Cleanup;
using Fookbase.Media.Infrastructure.IntegrationEvents;
using Fookbase.Media.Infrastructure.Persistence;
using Fookbase.Media.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace Fookbase.Media.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMediaInfrastructure(
        this IServiceCollection services,
        string connectionString,
        MinioOptions minioOptions,
        RabbitMqOptions rabbitMqOptions,
        OutboxOptions outboxOptions,
        MediaOptions mediaOptions)
    {
        minioOptions.Validate();
        rabbitMqOptions.Validate();
        outboxOptions.Validate();
        mediaOptions.Validate();

        services.AddDbContext<MediaDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton(minioOptions);
        services.AddSingleton(rabbitMqOptions);
        services.AddSingleton(outboxOptions);
        services.AddSingleton(mediaOptions);
        services.AddSingleton<IMinioClient>(_ =>
        {
            var client = new MinioClient()
                .WithEndpoint(minioOptions.Endpoint)
                .WithCredentials(minioOptions.AccessKey, minioOptions.SecretKey);
            if (minioOptions.Secure)
            {
                client = client.WithSSL();
            }

            return client.Build();
        });
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IMediaRepository, MediaRepository>();
        services.AddScoped<IObjectStorage, MinioObjectStorage>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<MediaProjectionStore>();
        services.AddScoped<IIntegrationEventPublisher, RabbitMqIntegrationEventPublisher>();
        services.AddHostedService<MinioBucketInitializer>();
        services.AddHostedService<ProjectionConsumer>();
        services.AddHostedService<OutboxPublisherWorker>();
        services.AddHostedService<PendingUploadCleanupWorker>();
        services.AddHostedService<ObjectDeletionWorker>();
        return services;
    }
}
