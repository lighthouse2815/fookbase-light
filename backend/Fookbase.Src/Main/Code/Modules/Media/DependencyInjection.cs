using Fookbase.Api.Modules.Media.Background;
using Fookbase.Api.Modules.Media.Config;
using Fookbase.Api.Modules.Media.Services;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace Fookbase.Api.Modules.Media;

public static class DependencyInjection
{
    public static IServiceCollection AddMediaInfrastructure(
        this IServiceCollection services,
        MinioOptions minioOptions,
        MediaOptions mediaOptions)
    {
        minioOptions.Validate();
        mediaOptions.Validate();

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
        services.AddSingleton<IVideoProcessor, FfmpegVideoProcessor>();
        services.AddScoped<MediaService>();
        services.AddHostedService<MinioBucketInitializer>();
        services.AddHostedService<PendingUploadCleanupWorker>();
        services.AddHostedService<ObjectDeletionWorker>();
        if (mediaOptions.VideoProcessingEnabled)
        {
            services.AddHostedService<VideoProcessingWorker>();
        }
        return services;
    }
}
