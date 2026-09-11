namespace Fookbase.Api.Modules.Media.Config;

public sealed class MediaOptions
{
    public const string SectionName = "Media";

    public long MaximumImageSizeBytes { get; init; } = 20L * 1024 * 1024;
    public long MaximumVideoSizeBytes { get; init; } = 500L * 1024 * 1024;
    public int UploadUrlExpiryMinutes { get; init; } = 15;
    public int DownloadUrlExpiryMinutes { get; init; } = 5;
    public int CleanupIntervalSeconds { get; init; } = 60;
    public int CleanupBatchSize { get; init; } = 100;
    public bool VideoProcessingEnabled { get; init; } = true;
    public int VideoProcessingIntervalSeconds { get; init; } = 5;
    public int VideoProcessingBatchSize { get; init; } = 1;
    public int VideoProcessingTimeoutSeconds { get; init; } = 120;
    public int VideoProcessingRetryLimit { get; init; } = 3;
    public int VideoProcessingRetryDelaySeconds { get; init; } = 15;
    public int MinimumReelDurationMs { get; init; } = 1_000;
    public int MaximumReelDurationMs { get; init; } = 180_000;

    public void Validate()
    {
        if (MaximumImageSizeBytes <= 0 || MaximumVideoSizeBytes <= 0 ||
            UploadUrlExpiryMinutes <= 0 || DownloadUrlExpiryMinutes <= 0 ||
            CleanupIntervalSeconds <= 0 || CleanupBatchSize <= 0 ||
            VideoProcessingIntervalSeconds <= 0 || VideoProcessingBatchSize <= 0 ||
            VideoProcessingTimeoutSeconds <= 0 || VideoProcessingRetryLimit <= 0 ||
            VideoProcessingRetryDelaySeconds <= 0 || MinimumReelDurationMs <= 0 ||
            MaximumReelDurationMs < MinimumReelDurationMs)
        {
            throw new InvalidOperationException("Media size, expiry and cleanup settings must be positive.");
        }
    }
}
