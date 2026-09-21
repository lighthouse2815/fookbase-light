namespace Fookbase.Api.Modules.Media.Entities;

public enum MediaType
{
    Image,
    Video
}

public enum MediaStatus
{
    PendingUpload,
    Ready,
    Failed,
    Deleted,
    Processing
}

public sealed class MediaAsset
{
    private MediaAsset() { }

    private MediaAsset(
        Guid id,
        Guid ownerUserId,
        MediaType mediaType,
        string objectKey,
        string originalFileName,
        string contentType,
        long declaredSizeBytes,
        DateTimeOffset createdAtUtc,
        DateTimeOffset uploadExpiresAtUtc)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        MediaType = mediaType;
        Status = MediaStatus.PendingUpload;
        ObjectKey = objectKey;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        DeclaredSizeBytes = declaredSizeBytes;
        CreatedAtUtc = createdAtUtc;
        UploadExpiresAtUtc = uploadExpiresAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public MediaType MediaType { get; private set; }
    public MediaStatus Status { get; private set; }
    public string ObjectKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long DeclaredSizeBytes { get; private set; }
    public long? ActualSizeBytes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UploadExpiresAtUtc { get; private set; }
    public DateTimeOffset? UploadedAtUtc { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }
    public long? DurationMs { get; private set; }
    public int? Width { get; private set; }
    public int? Height { get; private set; }
    public string? ProcessedObjectKey { get; private set; }
    public string? PosterObjectKey { get; private set; }
    public string? ProcessingError { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public static MediaAsset CreatePending(
        Guid id,
        Guid ownerUserId,
        MediaType mediaType,
        string objectKey,
        string originalFileName,
        string contentType,
        long declaredSizeBytes,
        DateTimeOffset createdAtUtc,
        DateTimeOffset uploadExpiresAtUtc) =>
        new(id, ownerUserId, mediaType, objectKey, originalFileName, contentType,
            declaredSizeBytes, createdAtUtc, uploadExpiresAtUtc);

    public bool MarkReady(long actualSizeBytes, DateTimeOffset uploadedAtUtc)
    {
        if (Status == MediaStatus.Ready)
        {
            return false;
        }

        if (Status != MediaStatus.PendingUpload)
        {
            throw new InvalidOperationException("Only a pending upload can become ready.");
        }

        ActualSizeBytes = actualSizeBytes;
        UploadedAtUtc = uploadedAtUtc;
        Status = MediaStatus.Ready;
        return true;
    }

    public bool MarkFailed()
    {
        if (Status is not (MediaStatus.PendingUpload or MediaStatus.Processing))
        {
            return false;
        }

        Status = MediaStatus.Failed;
        return true;
    }

    public void MarkProcessing(long actualSizeBytes, DateTimeOffset uploadedAtUtc)
    {
        if (Status != MediaStatus.PendingUpload || MediaType != MediaType.Video)
        {
            throw new InvalidOperationException("Only a pending video upload can begin processing.");
        }

        ActualSizeBytes = actualSizeBytes;
        UploadedAtUtc = uploadedAtUtc;
        Status = MediaStatus.Processing;
    }

    public void MarkVideoReady(
        string processedObjectKey,
        string posterObjectKey,
        long durationMs,
        int width,
        int height,
        DateTimeOffset processedAtUtc)
    {
        if (Status != MediaStatus.Processing || MediaType != MediaType.Video ||
            durationMs <= 0 || width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("Only a processed video with valid metadata can become ready.");
        }

        ProcessedObjectKey = processedObjectKey;
        PosterObjectKey = posterObjectKey;
        DurationMs = durationMs;
        Width = width;
        Height = height;
        ProcessedAtUtc = processedAtUtc;
        ProcessingError = null;
        Status = MediaStatus.Ready;
    }

    public void MarkProcessingFailed(string error)
    {
        if (Status != MediaStatus.Processing)
        {
            throw new InvalidOperationException("Only a processing video can fail processing.");
        }

        ProcessingError = error.Length <= 1000 ? error : error[..1000];
        Status = MediaStatus.Failed;
    }

    public static string ProcessedKey(Guid ownerUserId, Guid mediaId) =>
        $"{ownerUserId:N}/{mediaId:N}/processed.mp4";

    public static string PosterKey(Guid ownerUserId, Guid mediaId) =>
        $"{ownerUserId:N}/{mediaId:N}/poster.jpg";

    public bool Delete(DateTimeOffset deletedAtUtc)
    {
        if (Status == MediaStatus.Deleted)
        {
            return false;
        }

        if (Status != MediaStatus.Ready)
        {
            throw new InvalidOperationException("Only ready media can be deleted.");
        }

        Status = MediaStatus.Deleted;
        DeletedAtUtc = deletedAtUtc;
        return true;
    }
}
