using Fookbase.Api.Modules.Media.Config;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Media.Services;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Media.Background;

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
                var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
                var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
                var now = timeProvider.GetUtcNow();
                var jobs = await db.ObjectDeletions
                    .Where(x => x.ProcessedAtUtc == null && x.FailedAtUtc == null && x.NextAttemptAtUtc <= now)
                    .OrderBy(x => x.NextAttemptAtUtc).ThenBy(x => x.CreatedAtUtc)
                    .Take(options.CleanupBatchSize)
                    .ToListAsync(stoppingToken);
                foreach (var job in jobs)
                {
                    try
                    {
                        var asset = await db.MediaAssets.AsNoTracking()
                            .SingleOrDefaultAsync(asset => asset.Id == job.MediaId, stoppingToken);
                        if (asset is null)
                            throw new InvalidOperationException("Media asset for object deletion was not found.");
                        var mediaType = job.ObjectKey == asset.ObjectKey ? asset.MediaType
                            : job.ObjectKey == MediaAsset.ProcessedKey(asset.OwnerUserId, asset.Id) ? MediaType.VIDEO
                            : job.ObjectKey == MediaAsset.PosterKey(asset.OwnerUserId, asset.Id) ? MediaType.IMAGE
                            : throw new InvalidOperationException("Object deletion key does not belong to its media asset.");
                        await storage.DeleteAsync(job.ObjectKey, mediaType, stoppingToken);
                        job.MarkProcessed(timeProvider.GetUtcNow());
                    }
                    catch (Exception exception)
                    {
                        var failedAt = timeProvider.GetUtcNow();
                        job.RecordFailure(
                            failedAt,
                            failedAt.AddSeconds(options.ObjectDeletionRetryDelaySeconds),
                            options.ObjectDeletionRetryLimit,
                            exception.Message);
                        logger.LogWarning(exception,
                            "Object deletion {DeletionId} failed on attempt {Attempt}; it is {State}.",
                            job.Id,
                            job.RetryCount,
                            job.FailedAtUtc is null ? "scheduled for retry" : "permanently failed");
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
