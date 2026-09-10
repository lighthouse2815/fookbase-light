using Fookbase.Api.Modules.Notifications.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api.Modules.Notifications;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<NotificationService>();
        return services;
    }
}
