namespace Fookbase.Api.Shared.Contracts.Friends;

public sealed record FriendRequestSentIntegrationEvent(
    Guid EventId,
    Guid RequestId,
    Guid SenderUserId,
    Guid ReceiverUserId,
    DateTimeOffset OccurredAtUtc)
{
    public const string EventType = "friends.request.sent.v1";
}

public sealed record FriendRequestAcceptedIntegrationEvent(
    Guid EventId,
    Guid RequestId,
    Guid UserId1,
    Guid UserId2,
    DateTimeOffset OccurredAtUtc)
{
    public const string EventType = "friends.request.accepted.v1";
}

public sealed record FriendshipRemovedIntegrationEvent(
    Guid EventId,
    Guid UserId1,
    Guid UserId2,
    DateTimeOffset OccurredAtUtc)
{
    public const string EventType = "friends.friendship.removed.v1";
}

public sealed record UserBlockedIntegrationEvent(
    Guid EventId,
    Guid BlockerUserId,
    Guid BlockedUserId,
    DateTimeOffset OccurredAtUtc)
{
    public const string EventType = "friends.user.blocked.v1";
}

public sealed record UserUnblockedIntegrationEvent(
    Guid EventId,
    Guid BlockerUserId,
    Guid BlockedUserId,
    DateTimeOffset OccurredAtUtc)
{
    public const string EventType = "friends.user.unblocked.v1";
}
