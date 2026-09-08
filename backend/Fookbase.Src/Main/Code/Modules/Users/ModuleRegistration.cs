using Fookbase.Api.Modules.Users.Services;
using Fookbase.Api.Modules.Users.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api.Modules.Users;

public static class DependencyInjection
{
    public static IServiceCollection AddUsersInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<UsersDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IUserRegistrationStore, UserRegistrationStore>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IUserRegisteredEventHandler, UserRegisteredEventHandler>();

        return services;
    }
}
