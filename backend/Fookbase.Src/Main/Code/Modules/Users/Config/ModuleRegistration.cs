using Fookbase.Api.Modules.Users.Services;
using Fookbase.Api.Modules.Users.Data;
using Fookbase.Api.Modules.Users.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api.Modules.Users.Config;

public static class DependencyInjection
{
    public static IServiceCollection AddUsersInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<UsersDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<UserProfileService>();
        services.AddScoped<UserRegisteredEventHandler>();

        return services;
    }
}
