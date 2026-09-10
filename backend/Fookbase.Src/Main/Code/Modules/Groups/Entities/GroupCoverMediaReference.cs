namespace Fookbase.Api.Modules.Groups.Entities;

public sealed class GroupCoverMediaReference
{
    private GroupCoverMediaReference()
    {
    }

    private GroupCoverMediaReference(Guid groupId, Guid mediaId, DateTimeOffset attachedAtUtc)
    {
        GroupId = groupId;
        MediaId = mediaId;
        AttachedAtUtc = attachedAtUtc;
    }

    public Guid GroupId { get; private set; }
    public Guid MediaId { get; private set; }
    public DateTimeOffset AttachedAtUtc { get; private set; }

    public static GroupCoverMediaReference Create(Guid groupId, Guid mediaId, DateTimeOffset attachedAtUtc) =>
        new(groupId, mediaId, attachedAtUtc);
}
