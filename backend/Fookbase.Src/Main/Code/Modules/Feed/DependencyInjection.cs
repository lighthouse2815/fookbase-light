using Fookbase.Api.Modules.Feed.Services;
using Fookbase.Api.Modules.Feed.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api.Modules.Feed;

public static class DependencyInjection
{
    public static IServiceCollection AddFeedInfrastructure(
        this IServiceCollection services,
        FeedRankingOptions options)
    {
        options.Validate();
        services.AddSingleton(options);
        services.AddDataProtection();
        services.AddScoped<FeedService>();
        return services;
    }
}
