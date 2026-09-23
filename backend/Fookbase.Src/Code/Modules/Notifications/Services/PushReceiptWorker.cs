using Fookbase.Api.Modules.Notifications.Config;

namespace Fookbase.Api.Modules.Notifications.Services;

public sealed class PushReceiptWorker(
    IServiceScopeFactory scopeFactory,
    PushNotificationOptions options,
    ILogger<PushReceiptWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.ReceiptCheckIntervalMinutes));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<PushNotificationService>()
                    .CheckReceiptsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unexpected Expo push receipt worker failure.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
