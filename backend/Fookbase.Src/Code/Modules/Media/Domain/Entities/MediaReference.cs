namespace Fookbase.Api.Modules.Media.Entities;

public sealed class MediaReference
{
    private MediaReference() { }

    public MediaReference(Guid mediaId, Guid postId, DateTimeOffset attachedAtUtc)
    {
        MediaId = mediaId;
        PostId = postId;
        AttachedAtUtc = attachedAtUtc;
    }

    public Guid MediaId { get; private set; }
    public Guid PostId { get; private set; }
    public DateTimeOffset AttachedAtUtc { get; private set; }

}
