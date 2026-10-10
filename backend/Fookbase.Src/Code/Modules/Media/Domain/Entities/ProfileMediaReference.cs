using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Media.Entities;

[PrimaryKey(nameof(UserId), nameof(Slot))]
[Index(nameof(MediaId))]
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

    [ForeignKey(nameof(UserId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public User User { get; private set; } = null!;

    [ForeignKey(nameof(MediaId))]
    [InverseProperty(nameof(MediaAsset.ProfileReferences))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset Media { get; private set; } = null!;

}
