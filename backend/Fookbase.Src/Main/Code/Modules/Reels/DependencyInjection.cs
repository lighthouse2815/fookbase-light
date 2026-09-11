using Fookbase.Api.Modules.Reels.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api.Modules.Reels;

public static class DependencyInjection
{
    public static IServiceCollection AddReelsInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ReelsService>();
        return services;
    }
}
