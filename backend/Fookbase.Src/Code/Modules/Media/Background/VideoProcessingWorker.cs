using Fookbase.Api.Modules.Media.Config;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Media.Services;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Media.Background;

internal sealed class VideoProcessingWorker(
    IServiceScopeFactory scopeFactory,
    MediaOptions options,
    TimeProvider timeProvider,
    ILogger<VideoProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobsToRun = Math.Min(options.VideoProcessingBatchSize, options.MaxConcurrentJobs);
                await Task.WhenAll(Enumerable.Range(0, jobsToRun)
                    .Select(_ => ProcessNextAsync(stoppingToken)));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Video processing reconciliation failed; pending jobs will retry.");
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(options.VideoProcessingIntervalSeconds),
                    timeProvider,
                    stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessNextAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var now = timeProvider.GetUtcNow();
        var staleBefore = now.AddSeconds(-options.VideoProcessingTimeoutSeconds);
        var candidate = await db.MediaProcessingJobs.AsNoTracking()
            .Where(item =>
                (item.Status == MediaProcessingJobStatus.PENDING && item.NextAttemptAtUtc <= now) ||
                (item.Status == MediaProcessingJobStatus.PROCESSING &&
                 item.StartedAtUtc != null && item.StartedAtUtc < staleBefore))
            .OrderBy(item => item.NextAttemptAtUtc)
            .ThenBy(item => item.CreatedAtUtc)
            .Select(item => new { item.Id })
            .FirstOrDefaultAsync(stoppingToken);
        if (candidate is null)
        {
            return false;
        }

        var claimed = await db.MediaProcessingJobs
            .Where(item => item.Id == candidate.Id &&
                ((item.Status == MediaProcessingJobStatus.PENDING && item.NextAttemptAtUtc <= now) ||
                 (item.Status == MediaProcessingJobStatus.PROCESSING &&
                  item.StartedAtUtc != null && item.StartedAtUtc < staleBefore)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, MediaProcessingJobStatus.PROCESSING)
                .SetProperty(item => item.AttemptCount, item => item.AttemptCount + 1)
                .SetProperty(item => item.StartedAtUtc, now)
                .SetProperty(item => item.LastError, (string?)null), stoppingToken);
        if (claimed == 0)
        {
            return true;
        }

        var job = await db.MediaProcessingJobs.SingleAsync(item => item.Id == candidate.Id, stoppingToken);

        var asset = await db.MediaAssets.SingleOrDefaultAsync(asset => asset.Id == job.MediaId, stoppingToken);
        if (asset is null || asset.Status != MediaStatus.PROCESSING || asset.MediaType != MediaType.VIDEO)
        {
            job.Fail(timeProvider.GetUtcNow(), "The media asset is no longer available for processing.");
            await db.SaveChangesAsync(stoppingToken);
            return true;
        }

        var jobDirectory = Path.Combine(Path.GetTempPath(), "fookbase-video-processing", job.Id.ToString("N"));
        var inputPath = Path.Combine(jobDirectory, "input");
        var normalizedPath = Path.Combine(jobDirectory, "normalized.mp4");
        var posterPath = Path.Combine(jobDirectory, "poster.jpg");
        try
        {
            Directory.CreateDirectory(jobDirectory);
            var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
            var processor = scope.ServiceProvider.GetRequiredService<IVideoProcessor>();
            await storage.DownloadToFileAsync(asset.ObjectKey, MediaType.VIDEO, inputPath, stoppingToken);
            var metadata = await processor.ProcessAsync(
                inputPath,
                normalizedPath,
                posterPath,
                TimeSpan.FromSeconds(options.VideoProcessingTimeoutSeconds),
                stoppingToken);
            var processedKey = MediaAsset.ProcessedKey(asset.OwnerUserId, asset.Id);
            var posterKey = MediaAsset.PosterKey(asset.OwnerUserId, asset.Id);
            await storage.UploadFileAsync(processedKey, MediaType.VIDEO, normalizedPath, "video/mp4", stoppingToken);
            await storage.UploadFileAsync(posterKey, MediaType.IMAGE, posterPath, "image/jpeg", stoppingToken);
            var completedAt = timeProvider.GetUtcNow();
            asset.MarkVideoReady(
                processedKey,
                posterKey,
                metadata.DurationMs,
                metadata.Width,
                metadata.Height,
                completedAt);
            job.Succeed(completedAt);
            await db.SaveChangesAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var completedAt = timeProvider.GetUtcNow();
            logger.LogWarning(exception, "Video processing job {JobId} attempt {Attempt} failed.", job.Id, job.AttemptCount);
            if (job.AttemptCount >= options.VideoProcessingRetryLimit)
            {
                asset.MarkProcessingFailed("Video processing failed.");
                job.Fail(completedAt, exception.Message);
                db.ObjectDeletions.Add(ObjectDeletion.Create(asset.Id, asset.ObjectKey, completedAt));
                db.ObjectDeletions.Add(ObjectDeletion.Create(
                    asset.Id, MediaAsset.ProcessedKey(asset.OwnerUserId, asset.Id), completedAt));
                db.ObjectDeletions.Add(ObjectDeletion.Create(
                    asset.Id, MediaAsset.PosterKey(asset.OwnerUserId, asset.Id), completedAt));
            }
            else
            {
                job.Retry(completedAt.AddSeconds(options.VideoProcessingRetryDelaySeconds), exception.Message);
            }

            await db.SaveChangesAsync(stoppingToken);
        }
        finally
        {
            if (Directory.Exists(jobDirectory))
            {
                try
                {
                    Directory.Delete(jobDirectory, recursive: true);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Could not remove temporary video directory for job {JobId}.", job.Id);
                }
            }
        }

        return true;
    }
}
