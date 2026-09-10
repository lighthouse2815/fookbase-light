using Fookbase.Api.Modules.Messages.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR;
using Fookbase.Api.Modules.Messages.Hubs;

namespace Fookbase.Api.Modules.Messages;

public static class DependencyInjection
{
    public static IServiceCollection AddMessagesInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();
        services.AddScoped<MessagesService>();

        return services;
    }
}
