namespace Fookbase.Media.Domain.Entities;

public sealed class MediaReference
{
    private MediaReference() { }

    private MediaReference(Guid mediaId, Guid postId, DateTimeOffset attachedAtUtc)
    {
        MediaId = mediaId;
        PostId = postId;
        AttachedAtUtc = attachedAtUtc;
    }

    public Guid MediaId { get; private set; }
    public Guid PostId { get; private set; }
    public DateTimeOffset AttachedAtUtc { get; private set; }

    public static MediaReference Create(Guid mediaId, Guid postId, DateTimeOffset attachedAtUtc) =>
        new(mediaId, postId, attachedAtUtc);
}
