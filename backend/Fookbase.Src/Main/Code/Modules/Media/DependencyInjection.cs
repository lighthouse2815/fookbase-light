using Fookbase.Api.Modules.Media.Background;
using Fookbase.Api.Modules.Media.Config;
using Fookbase.Api.Modules.Media.Services;
using CloudinaryDotNet;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api.Modules.Media;

public static class DependencyInjection
{
    public static IServiceCollection AddMediaInfrastructure(
        this IServiceCollection services,
        CloudinaryOptions cloudinaryOptions,
        MediaOptions mediaOptions)
    {
        cloudinaryOptions.Validate();
        mediaOptions.Validate();

        services.AddSingleton(cloudinaryOptions);
        services.AddSingleton(mediaOptions);
        services.AddSingleton(_ => new Cloudinary(new Account(
            cloudinaryOptions.CloudName, cloudinaryOptions.ApiKey, cloudinaryOptions.ApiSecret)));
        services.AddHttpClient();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IObjectStorage, CloudinaryObjectStorage>();
        services.AddSingleton<IVideoProcessor, FfmpegVideoProcessor>();
        services.AddScoped<MediaService>();
        services.AddHostedService<PendingUploadCleanupWorker>();
        services.AddHostedService<ObjectDeletionWorker>();
        if (mediaOptions.VideoProcessingEnabled)
        {
            services.AddHostedService<VideoProcessingWorker>();
        }
        return services;
    }

}
