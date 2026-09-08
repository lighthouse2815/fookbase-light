namespace Fookbase.Api.Modules.Media.Services;

public sealed class MediaOptions
{
    public const string SectionName = "Media";

    public long MaximumImageSizeBytes { get; init; } = 20L * 1024 * 1024;
    public long MaximumVideoSizeBytes { get; init; } = 500L * 1024 * 1024;
    public int UploadUrlExpiryMinutes { get; init; } = 15;
    public int DownloadUrlExpiryMinutes { get; init; } = 5;
    public int CleanupIntervalSeconds { get; init; } = 60;
    public int CleanupBatchSize { get; init; } = 100;

    public void Validate()
    {
        if (MaximumImageSizeBytes <= 0 || MaximumVideoSizeBytes <= 0 ||
            UploadUrlExpiryMinutes <= 0 || DownloadUrlExpiryMinutes <= 0 ||
            CleanupIntervalSeconds <= 0 || CleanupBatchSize <= 0)
        {
            throw new InvalidOperationException("Media size, expiry and cleanup settings must be positive.");
        }
    }
}
