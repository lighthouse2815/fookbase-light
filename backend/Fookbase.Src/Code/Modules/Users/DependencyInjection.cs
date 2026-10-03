using Fookbase.Api.Modules.Users.Services;

namespace Fookbase.Api.Modules.Users;

public static class DependencyInjection
{
    public static IServiceCollection AddUsersInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<UserProfileService>();
        services.AddScoped<UserPrivacySettingsService>();

        return services;
    }
}
