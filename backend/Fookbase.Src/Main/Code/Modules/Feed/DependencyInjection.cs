using Fookbase.Api.Modules.Feed.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api.Modules.Feed;

public static class DependencyInjection
{
    public static IServiceCollection AddFeedInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<FeedService>();
        return services;
    }
}
