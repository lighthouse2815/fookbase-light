using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Media.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace Fookbase.Api.Modules.Media.Config;

public static class DependencyInjection
{
    public static IServiceCollection AddMediaInfrastructure(
        this IServiceCollection services,
        string connectionString,
        MinioOptions minioOptions,
        MediaOptions mediaOptions)
    {
        minioOptions.Validate();
        mediaOptions.Validate();

        services.AddDbContext<MediaDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton(minioOptions);
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
        services.AddScoped<IObjectStorage, MinioObjectStorage>();
        services.AddScoped<MediaService>();
        services.AddHostedService<MinioBucketInitializer>();
        services.AddHostedService<PendingUploadCleanupWorker>();
        services.AddHostedService<ObjectDeletionWorker>();
        return services;
    }
}
