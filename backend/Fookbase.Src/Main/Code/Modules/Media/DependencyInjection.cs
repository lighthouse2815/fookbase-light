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
        services.AddSingleton<IMinioClient>(_ => CreateClient(minioOptions, minioOptions.Endpoint));
        services.AddSingleton<MinioPresignedUrlClient>(_ =>
            new(CreateClient(minioOptions, minioOptions.PresignedUrlEndpoint)));
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

    private static IMinioClient CreateClient(MinioOptions options, string endpoint)
    {
        var client = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(options.AccessKey, options.SecretKey);
        if (options.Secure)
        {
            client = client.WithSSL();
        }

        return client.Build();
    }
}
