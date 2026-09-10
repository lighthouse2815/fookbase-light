namespace Fookbase.Api.Modules.Groups.Entities;

public enum GroupMemberRole
{
    Owner,
    Admin,
    Moderator,
    Member
}

public sealed class GroupMember
{
    private GroupMember()
    {
    }

    private GroupMember(
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

    public static GroupMember Create(
        Guid groupId,
        Guid userId,
        GroupMemberRole role,
        DateTimeOffset joinedAtUtc) =>
        new(groupId, userId, role, joinedAtUtc);

    public void ChangeRole(GroupMemberRole role)
    {
        Role = role;
    }
}
