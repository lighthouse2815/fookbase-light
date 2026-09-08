using Fookbase.Users.Application.Abstractions;
using Fookbase.Users.Application.Profiles;
using Fookbase.Users.Application.Registrations;
using Fookbase.Users.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Users.Infrastructure;

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
