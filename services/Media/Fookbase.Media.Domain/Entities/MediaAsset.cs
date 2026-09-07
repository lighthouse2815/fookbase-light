namespace Fookbase.Media.Domain.Entities;

public enum MediaPurpose
{
    Avatar,
    Cover,
    Post
}

public sealed class MediaAsset
{
    private MediaAsset()
    {
    }

    private MediaAsset(
        Guid id,
        Guid ownerUserId,
        string objectName,
        string originalFileName,
        string contentType,
        long size,
        MediaPurpose purpose,
        DateTimeOffset createdAt)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        ObjectName = objectName;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        Size = size;
        Purpose = purpose;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid OwnerUserId { get; private set; }

    public string ObjectName { get; private set; } = string.Empty;

    public string OriginalFileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long Size { get; private set; }

    public MediaPurpose Purpose { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public static MediaAsset Create(
        Guid id,
        Guid ownerUserId,
        string objectName,
        string originalFileName,
        string contentType,
        long size,
        MediaPurpose purpose,
        DateTimeOffset createdAt) =>
        new(
            id,
            ownerUserId,
            objectName,
            originalFileName,
            contentType,
            size,
            purpose,
            createdAt);

    public void Delete(DateTimeOffset deletedAt)
    {
        DeletedAt ??= deletedAt;
    }
}
