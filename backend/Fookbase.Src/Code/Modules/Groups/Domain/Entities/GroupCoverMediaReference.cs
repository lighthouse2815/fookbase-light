using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Groups.Entities;

[Index(nameof(MediaId))]
public sealed class GroupCoverMediaReference
{
    private GroupCoverMediaReference() { }

    public GroupCoverMediaReference(Guid groupId, Guid mediaId, DateTimeOffset attachedAtUtc)
    {
        GroupId = groupId;
        MediaId = mediaId;
        AttachedAtUtc = attachedAtUtc;
    }

    [Key]
    public Guid GroupId { get; private set; }

    [InverseProperty(nameof(Group.CoverMediaReference))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Group Group { get; private set; } = null!;

    public Guid MediaId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset Media { get; private set; } = null!;

    public DateTimeOffset AttachedAtUtc { get; private set; }
}
