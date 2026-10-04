using Fookbase.Api.Modules.Groups.Domain.Enums;

namespace Fookbase.Api.Modules.Groups.Entities;

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

    public void ChangeRole(GroupMemberRole role)
    {
        Role = role;
    }
}
