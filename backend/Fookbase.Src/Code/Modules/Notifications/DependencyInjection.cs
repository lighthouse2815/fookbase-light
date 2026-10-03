using Fookbase.Api.Modules.Notifications.Config;
using Fookbase.Api.Modules.Notifications.Services;

namespace Fookbase.Api.Modules.Notifications;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsInfrastructure(
        this IServiceCollection services,
        PushNotificationOptions pushNotificationOptions)
    {
        pushNotificationOptions.Validate();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(pushNotificationOptions);
        services.AddHttpClient("expo-push", client =>
        {
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<NotificationService>();
        services.AddScoped<PushNotificationService>();
        services.AddHostedService<PushReceiptWorker>();
        return services;
    }
}
