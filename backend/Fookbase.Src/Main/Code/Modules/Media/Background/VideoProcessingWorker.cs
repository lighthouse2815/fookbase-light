using Fookbase.Api.Modules.Media.Config;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
                for (var index = 0; index < options.VideoProcessingBatchSize; index++)
                {
                    if (!await ProcessNextAsync(stoppingToken))
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Video processing reconciliation failed; pending jobs will retry.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(options.VideoProcessingIntervalSeconds),
                timeProvider,
                stoppingToken);
        }
    }

    private async Task<bool> ProcessNextAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var now = timeProvider.GetUtcNow();
        var staleBefore = now.AddSeconds(-options.VideoProcessingTimeoutSeconds);
        var job = await db.MediaProcessingJobs
            .Where(item =>
                (item.Status == MediaProcessingJobStatus.Pending && item.NextAttemptAtUtc <= now) ||
                (item.Status == MediaProcessingJobStatus.Processing &&
                 item.StartedAtUtc != null && item.StartedAtUtc < staleBefore))
            .OrderBy(item => item.NextAttemptAtUtc)
            .ThenBy(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(stoppingToken);
        if (job is null)
        {
            return false;
        }

        job.Claim(now);
        await db.SaveChangesAsync(stoppingToken);

        var asset = await db.MediaAssets.SingleOrDefaultAsync(asset => asset.Id == job.MediaId, stoppingToken);
        if (asset is null || asset.Status != MediaStatus.Processing || asset.MediaType != MediaType.Video)
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
            await storage.DownloadToFileAsync(asset.ObjectKey, inputPath, stoppingToken);
            var metadata = await processor.ProcessAsync(
                inputPath,
                normalizedPath,
                posterPath,
                TimeSpan.FromSeconds(options.VideoProcessingTimeoutSeconds),
                stoppingToken);
            var processedKey = MediaAsset.ProcessedKey(asset.OwnerUserId, asset.Id);
            var posterKey = MediaAsset.PosterKey(asset.OwnerUserId, asset.Id);
            await storage.UploadFileAsync(processedKey, normalizedPath, "video/mp4", stoppingToken);
            await storage.UploadFileAsync(posterKey, posterPath, "image/jpeg", stoppingToken);
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
                Directory.Delete(jobDirectory, recursive: true);
            }
        }

        return true;
    }
}
