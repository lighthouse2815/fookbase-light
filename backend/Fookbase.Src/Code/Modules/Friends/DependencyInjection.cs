using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Friends.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api.Modules.Friends;

public static class DependencyInjection
{
    public static IServiceCollection AddFriendsInfrastructure(
        this IServiceCollection services,
        FriendSuggestionOptions friendSuggestionOptions)
    {
        friendSuggestionOptions.Validate();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(friendSuggestionOptions);
        services.AddScoped<FriendsService>();
        services.AddScoped<FriendSuggestionService>();

        return services;
    }
}
