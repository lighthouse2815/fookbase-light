using Fookbase.Api.Modules.Friends.Services.Abstractions;
using Fookbase.Api.Modules.Friends.Services.Registrations;
using Fookbase.Api.Modules.Friends.Services.Relationships;
using Fookbase.Api.Modules.Friends.Services.IntegrationEvents;
using Fookbase.Api.Modules.Friends.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api.Modules.Friends;

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
