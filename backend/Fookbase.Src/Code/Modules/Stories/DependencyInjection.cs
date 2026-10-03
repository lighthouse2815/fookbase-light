using Fookbase.Api.Modules.Stories.Config;
using Fookbase.Api.Modules.Stories.Services;

namespace Fookbase.Api.Modules.Stories;

public static class DependencyInjection
{
    public static IServiceCollection AddStoriesInfrastructure(
        this IServiceCollection services,
        StoriesOptions options)
    {
        options.Validate();
        services.AddSingleton(options);
        services.AddScoped<StoriesService>();
        return services;
    }
}
