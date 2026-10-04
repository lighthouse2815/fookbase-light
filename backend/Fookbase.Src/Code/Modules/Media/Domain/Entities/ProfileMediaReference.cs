using Fookbase.Api.Modules.Media.Domain.Enums;

namespace Fookbase.Api.Modules.Media.Entities;

public sealed class ProfileMediaReference
{
    private ProfileMediaReference()
    {
    }

    public ProfileMediaReference(
        Guid userId,
        ProfileMediaSlot slot,
        Guid mediaId,
        DateTimeOffset attachedAtUtc)
    {
        UserId = userId;
        Slot = slot;
        MediaId = mediaId;
        AttachedAtUtc = attachedAtUtc;
    }

    public Guid UserId { get; private set; }

    public ProfileMediaSlot Slot { get; private set; }

    public Guid MediaId { get; private set; }

    public DateTimeOffset AttachedAtUtc { get; private set; }

}
