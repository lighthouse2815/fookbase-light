using Fookbase.Api.Modules.Media.Config;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Media.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fookbase.Api.Modules.Media.Services;

internal sealed class ObjectDeletionWorker(
    IServiceScopeFactory scopeFactory, MediaOptions options, TimeProvider timeProvider,
    ILogger<ObjectDeletionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
                var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
                var jobs = await db.ObjectDeletions.Where(x => x.ProcessedAtUtc == null)
                    .OrderBy(x => x.CreatedAtUtc).Take(options.CleanupBatchSize).ToListAsync(stoppingToken);
                foreach (var job in jobs)
                {
                    try
                    {
                        await storage.DeleteAsync(job.ObjectKey, stoppingToken);
                        job.MarkProcessed(timeProvider.GetUtcNow());
                    }
                    catch (Exception exception)
                    {
                        job.RecordFailure(exception.Message);
                        logger.LogWarning(exception, "Object deletion {DeletionId} remains pending.", job.Id);
                    }
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Object deletion reconciliation failed; it will retry.");
            }
            await Task.Delay(TimeSpan.FromSeconds(options.CleanupIntervalSeconds), timeProvider, stoppingToken);
        }
    }
}
