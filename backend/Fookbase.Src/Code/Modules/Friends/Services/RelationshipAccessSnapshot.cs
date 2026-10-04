namespace Fookbase.Api.Modules.Friends.Services;

public sealed record RelationshipAccessSnapshot(
    IReadOnlySet<Guid> FriendUserIds,
    IReadOnlySet<Guid> BlockedUserIds,
    IReadOnlySet<Guid> FollowedUserIds);
