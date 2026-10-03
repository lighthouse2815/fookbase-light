using Fookbase.Api.Modules.Photos.Services;

namespace Fookbase.Api.Modules.Photos;

public static class DependencyInjection
{
    public static IServiceCollection AddPhotosModule(this IServiceCollection services)
    {
        services.AddScoped<PhotoAccessService>();
        services.AddScoped<PhotosService>();
        return services;
    }
}
