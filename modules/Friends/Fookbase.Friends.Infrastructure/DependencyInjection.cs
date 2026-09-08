using Fookbase.Friends.Application.Abstractions;
using Fookbase.Friends.Application.Registrations;
using Fookbase.Friends.Application.Relationships;
using Fookbase.Friends.Infrastructure.IntegrationEvents;
using Fookbase.Friends.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Friends.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFriendsInfrastructure(
        this IServiceCollection services,
        string connectionString,
        OutboxOptions outboxOptions)
    {
        outboxOptions.Validate();

        services.AddDbContext<FriendsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton(outboxOptions);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IFriendsStore, FriendsStore>();
        services.AddScoped<IFriendsService, FriendsService>();
        services.AddScoped<IKnownUserRegistrationStore, KnownUserRegistrationStore>();
        services.AddScoped<IUserRegisteredEventHandler, UserRegisteredEventHandler>();
        services.AddHostedService<OutboxPublisherWorker>();

        return services;
    }
}
