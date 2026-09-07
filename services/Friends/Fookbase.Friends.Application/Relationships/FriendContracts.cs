namespace Fookbase.Friends.Application.Relationships;

public sealed record FriendRequestResponse(
    Guid Id,
    Guid SenderUserId,
    Guid ReceiverUserId,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RespondedAtUtc);

public sealed record FriendResponse(Guid UserId, DateTimeOffset FriendsSinceUtc);

public sealed record BlockedUserResponse(Guid UserId, DateTimeOffset BlockedAtUtc);

public sealed record RelationshipStatusResponse(
    Guid UserId,
    string Status,
    Guid? RequestId = null);

public sealed record MutualFriendsResponse(
    int Count,
    IReadOnlyList<Guid> UserIds,
    int Offset,
    int Limit);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Offset,
    int Limit,
    int Total);
