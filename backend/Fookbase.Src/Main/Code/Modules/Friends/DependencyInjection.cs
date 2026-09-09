using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Friends.Data;
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
        services.AddScoped<FriendsService>();
        services.AddScoped<IFriendsService>(provider => provider.GetRequiredService<FriendsService>());
        services.AddScoped<IUserRegisteredEventHandler, UserRegisteredEventHandler>();
        services.AddHostedService<OutboxPublisherWorker>();

        return services;
    }
}
