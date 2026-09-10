using Fookbase.Api.Modules.Friends.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api.Modules.Friends;

public static class DependencyInjection
{
    public static IServiceCollection AddFriendsInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<FriendsService>();

        return services;
    }
}
