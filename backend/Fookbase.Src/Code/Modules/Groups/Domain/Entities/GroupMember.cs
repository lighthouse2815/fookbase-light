using Fookbase.Api.Modules.Groups.Domain.Enums;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fookbase.Api.Modules.Groups.Entities;

[Table("GroupMembers")]
[PrimaryKey(nameof(GroupId), nameof(UserId))]
[Index(nameof(UserId), nameof(GroupId))]
[Index(nameof(GroupId), nameof(Role))]
public sealed class GroupMember
{
    private GroupMember()
    {
    }

    public GroupMember(
        Guid groupId,
        Guid userId,
        GroupMemberRole role,
        DateTimeOffset joinedAtUtc)
    {
        GroupId = groupId;
        UserId = userId;
        Role = role;
        JoinedAtUtc = joinedAtUtc;
    }

    public Guid GroupId { get; private set; }
    public Guid UserId { get; private set; }
    public GroupMemberRole Role { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; private set; }

    [ForeignKey(nameof(GroupId))]
    [InverseProperty(nameof(Group.Members))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Group Group { get; private set; } = null!;

    [ForeignKey(nameof(UserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User User { get; private set; } = null!;

    public void ChangeRole(GroupMemberRole role)
    {
        Role = role;
    }
}
