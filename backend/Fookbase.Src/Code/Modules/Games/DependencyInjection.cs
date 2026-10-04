using Fookbase.Api.Modules.Games.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fookbase.Api.Modules.Games;

public static class DependencyInjection
{
    public static IServiceCollection AddGamesInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddKeyedSingleton<GameRoomService>("flappy-bird");
        services.AddKeyedSingleton<GameRoomService>("jumping");
        return services;
    }
}
