namespace Fookbase.Api.Modules.Posts.Models;

public sealed class KnownMedia
{
    private KnownMedia() { }
    private KnownMedia(Guid mediaId, Guid ownerUserId, string mediaType, string contentType,
        long sizeBytes, DateTimeOffset changedAtUtc)
    {
        MediaId = mediaId; OwnerUserId = ownerUserId; MediaType = mediaType;
        ContentType = contentType; SizeBytes = sizeBytes; IsReady = true; LastChangedAtUtc = changedAtUtc;
    }
    public Guid MediaId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string MediaType { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public bool IsReady { get; private set; }
    public DateTimeOffset LastChangedAtUtc { get; private set; }
    public static KnownMedia Create(Guid id, Guid owner, string type, string contentType, long size,
        DateTimeOffset at) => new(id, owner, type, contentType, size, at);
    public static KnownMedia CreateDeleted(Guid id, Guid owner, DateTimeOffset at)
    {
        var media = new KnownMedia(id, owner, "unknown", "application/octet-stream", 0, at);
        media.IsReady = false;
        return media;
    }
    public void MarkReady(Guid owner, string type, string contentType, long size, DateTimeOffset at)
    {
        if (at < LastChangedAtUtc) return;
        OwnerUserId = owner; MediaType = type; ContentType = contentType;
        SizeBytes = size; IsReady = true; LastChangedAtUtc = at;
    }
    public void MarkDeleted(DateTimeOffset at)
    {
        if (at < LastChangedAtUtc) return;
        IsReady = false; LastChangedAtUtc = at;
    }
}
