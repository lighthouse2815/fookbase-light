namespace Fookbase.Api.Modules.Media.Models;

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
    Deleted
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
        if (Status != MediaStatus.PendingUpload)
        {
            return false;
        }

        Status = MediaStatus.Failed;
        return true;
    }

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
