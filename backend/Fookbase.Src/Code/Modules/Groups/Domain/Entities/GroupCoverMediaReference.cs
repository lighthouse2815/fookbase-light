using Fookbase.Api.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Groups.Entities;

[Table("GroupCoverMediaReferences")]
[Index(nameof(MediaId))]
public sealed class GroupCoverMediaReference
{
    private GroupCoverMediaReference()
    {
    }

    public GroupCoverMediaReference(Guid groupId, Guid mediaId, DateTimeOffset attachedAtUtc)
    {
        GroupId = groupId;
        MediaId = mediaId;
        AttachedAtUtc = attachedAtUtc;
    }

    [Key]
    public Guid GroupId { get; private set; }
    public Guid MediaId { get; private set; }
    public DateTimeOffset AttachedAtUtc { get; private set; }

    [ForeignKey(nameof(GroupId))]
    [InverseProperty(nameof(Group.CoverMediaReference))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Group Group { get; private set; } = null!;

    [ForeignKey(nameof(MediaId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset Media { get; private set; } = null!;

}
