using Fookbase.Media.Application.Abstractions;
using Fookbase.Media.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fookbase.Media.Infrastructure.IntegrationEvents;

internal sealed class OutboxPublisherWorker(
    IServiceScopeFactory scopeFactory, OutboxOptions options, TimeProvider timeProvider,
    ILogger<OutboxPublisherWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.PublisherEnabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            var any = await PublishAsync(stoppingToken);
            await Task.Delay(any ? TimeSpan.FromMilliseconds(100) :
                TimeSpan.FromSeconds(options.PollingIntervalSeconds), timeProvider, stoppingToken);
        }
    }

    private async Task<bool> PublishAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
            var messages = await db.OutboxMessages.Where(x => x.ProcessedAtUtc == null)
                .OrderBy(x => x.OccurredAtUtc).Take(options.BatchSize).ToListAsync(cancellationToken);
            foreach (var message in messages)
            {
                try
                {
                    await publisher.PublishAsync(new IntegrationEventMessage(
                        message.Id, message.Type, message.Payload, message.OccurredAtUtc), cancellationToken);
                    message.MarkProcessed(timeProvider.GetUtcNow());
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception exception)
                {
                    message.RecordFailure(exception.Message);
                    await db.SaveChangesAsync(cancellationToken);
                    logger.LogWarning(exception, "Media outbox publish failed; event {EventId} remains pending.", message.Id);
                    await Task.Delay(TimeSpan.FromSeconds(options.FailureBackoffSeconds), timeProvider, cancellationToken);
                    break;
                }
            }
            return messages.Count > 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return false; }
        catch (Exception exception)
        {
            logger.LogError(exception, "Media outbox polling failed; retrying.");
            return false;
        }
    }
}
