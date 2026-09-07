using Fookbase.Users.Application.Abstractions;
using Fookbase.Users.Application.Profiles;
using Fookbase.Users.Application.Registrations;
using Fookbase.Users.Infrastructure.IntegrationEvents;
using Fookbase.Users.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Users.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddUsersInfrastructure(
        this IServiceCollection services,
        string connectionString,
        RabbitMqOptions rabbitMqOptions)
    {
        rabbitMqOptions.Validate();

        services.AddDbContext<UsersDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddSingleton(rabbitMqOptions);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IUserRegistrationStore, UserRegistrationStore>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IUserRegisteredEventHandler, UserRegisteredEventHandler>();
        services.AddHostedService<UserRegisteredConsumer>();

        return services;
    }
}
