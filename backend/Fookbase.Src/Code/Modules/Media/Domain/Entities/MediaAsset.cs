using Fookbase.Api.Modules.Media.Domain.Enums;

namespace Fookbase.Api.Modules.Media.Entities;

public sealed class MediaAsset
{
    private MediaAsset() { }

    public MediaAsset(
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
        Status = MediaStatus.PENDING_UPLOAD;
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

    public bool MarkReady(long actualSizeBytes, DateTimeOffset uploadedAtUtc)
    {
        if (Status == MediaStatus.READY)
        {
            return false;
        }

        if (Status != MediaStatus.PENDING_UPLOAD)
        {
            throw new InvalidOperationException("Only a pending upload can become ready.");
        }

        ActualSizeBytes = actualSizeBytes;
        UploadedAtUtc = uploadedAtUtc;
        Status = MediaStatus.READY;
        return true;
    }

    public bool MarkFailed()
    {
        if (Status is not (MediaStatus.PENDING_UPLOAD or MediaStatus.PROCESSING))
        {
            return false;
        }

        Status = MediaStatus.FAILED;
        return true;
    }

    public void MarkProcessing(long actualSizeBytes, DateTimeOffset uploadedAtUtc)
    {
        if (Status != MediaStatus.PENDING_UPLOAD || MediaType != MediaType.VIDEO)
        {
            throw new InvalidOperationException("Only a pending video upload can begin processing.");
        }

        ActualSizeBytes = actualSizeBytes;
        UploadedAtUtc = uploadedAtUtc;
        Status = MediaStatus.PROCESSING;
    }

    public void MarkVideoReady(
        string processedObjectKey,
        string posterObjectKey,
        long durationMs,
        int width,
        int height,
        DateTimeOffset processedAtUtc)
    {
        if (Status != MediaStatus.PROCESSING || MediaType != MediaType.VIDEO ||
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
        Status = MediaStatus.READY;
    }

    public void MarkProcessingFailed(string error)
    {
        if (Status != MediaStatus.PROCESSING)
        {
            throw new InvalidOperationException("Only a processing video can fail processing.");
        }

        ProcessingError = error.Length <= 1000 ? error : error[..1000];
        Status = MediaStatus.FAILED;
    }

    public static string ProcessedKey(Guid ownerUserId, Guid mediaId) =>
        $"{ownerUserId:N}/{mediaId:N}/processed.mp4";

    public static string PosterKey(Guid ownerUserId, Guid mediaId) =>
        $"{ownerUserId:N}/{mediaId:N}/poster.jpg";

    public bool Delete(DateTimeOffset deletedAtUtc)
    {
        if (Status == MediaStatus.DELETED)
        {
            return false;
        }

        if (Status != MediaStatus.READY)
        {
            throw new InvalidOperationException("Only ready media can be deleted.");
        }

        Status = MediaStatus.DELETED;
        DeletedAtUtc = deletedAtUtc;
        return true;
    }
}
