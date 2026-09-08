using Fookbase.Api.Modules.Users.Services.Abstractions;
using Fookbase.Api.Modules.Users.Services.Profiles;
using Fookbase.Api.Modules.Users.Services.Registrations;
using Fookbase.Api.Modules.Users.Repositories;
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
