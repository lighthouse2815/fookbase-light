using Fookbase.Media.Application.Media;
using Fookbase.Media.Domain.Entities;
using Fookbase.Media.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fookbase.Media.Infrastructure.Cleanup;

internal sealed class PendingUploadCleanupWorker(
    IServiceScopeFactory scopeFactory, MediaOptions options, TimeProvider timeProvider,
    ILogger<PendingUploadCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
                var now = timeProvider.GetUtcNow();
                var assets = await db.MediaAssets.Where(x => x.Status == MediaStatus.PendingUpload &&
                        x.UploadExpiresAtUtc != null && x.UploadExpiresAtUtc <= now)
                    .OrderBy(x => x.UploadExpiresAtUtc).Take(options.CleanupBatchSize)
                    .ToListAsync(stoppingToken);
                foreach (var asset in assets)
                {
                    if (asset.MarkFailed())
                        db.ObjectDeletions.Add(ObjectDeletion.Create(asset.Id, asset.ObjectKey, now));
                }
                if (assets.Count > 0) await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Expired upload cleanup failed; it will retry.");
            }
            await Task.Delay(TimeSpan.FromSeconds(options.CleanupIntervalSeconds), timeProvider, stoppingToken);
        }
    }
}
