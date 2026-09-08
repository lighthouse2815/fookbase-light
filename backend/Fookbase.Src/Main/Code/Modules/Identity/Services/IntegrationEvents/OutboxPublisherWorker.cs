using Fookbase.Api.Modules.Identity.Services.Abstractions;
using Fookbase.Api.Modules.Identity.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fookbase.Api.Modules.Identity.Services.IntegrationEvents;

internal sealed class OutboxPublisherWorker(
    IServiceScopeFactory scopeFactory,
    OutboxOptions options,
    TimeProvider timeProvider,
    ILogger<OutboxPublisherWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.PublisherEnabled)
        {
            logger.LogInformation("Identity outbox publisher is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var publishedAny = await PublishPendingMessagesAsync(stoppingToken);
            var delay = publishedAny
                ? TimeSpan.FromMilliseconds(100)
                : TimeSpan.FromSeconds(options.PollingIntervalSeconds);

            await Task.Delay(delay, timeProvider, stoppingToken);
        }
    }

    private async Task<bool> PublishPendingMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
            var messages = await dbContext.OutboxMessages
                .Where(message => message.ProcessedAtUtc == null)
                .OrderBy(message => message.OccurredAtUtc)
                .Take(options.BatchSize)
                .ToListAsync(cancellationToken);

            foreach (var message in messages)
            {
                try
                {
                    await publisher.PublishAsync(
                        new IntegrationEventMessage(
                            message.Id,
                            message.Type,
                            message.Payload,
                            message.OccurredAtUtc),
                        cancellationToken);
                    message.MarkProcessed(timeProvider.GetUtcNow());
                    await dbContext.SaveChangesAsync(cancellationToken);
                    logger.LogInformation(
                        "Published integration event {EventId} ({EventType}) from the Identity outbox.",
                        message.Id,
                        message.Type);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    message.RecordFailure(exception.Message);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    logger.LogWarning(
                        exception,
                        "Could not publish integration event {EventId} ({EventType}); it remains pending.",
                        message.Id,
                        message.Type);
                    await Task.Delay(
                        TimeSpan.FromSeconds(options.FailureBackoffSeconds),
                        timeProvider,
                        cancellationToken);
                    break;
                }
            }

            return messages.Count > 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Identity outbox polling failed; the worker will retry.");
            return false;
        }
    }
}
