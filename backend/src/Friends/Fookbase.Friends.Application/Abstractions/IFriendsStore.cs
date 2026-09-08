using Fookbase.Friends.Application.Relationships;

namespace Fookbase.Friends.Application.Abstractions;

public enum FriendsStoreError
{
    None,
    UserNotFound,
    RequestNotFound,
    FriendshipNotFound,
    Forbidden,
    AlreadyFriends,
    PendingRequestExists,
    RelationshipBlocked,
    RequestNotPending
}

public sealed record FriendsStoreResult<T>(T? Value, FriendsStoreError Error)
{
    public bool Succeeded => Error == FriendsStoreError.None;

    public static FriendsStoreResult<T> Success(T value) =>
        new(value, FriendsStoreError.None);

    public static FriendsStoreResult<T> Failure(FriendsStoreError error) =>
        new(default, error);
}

public interface IFriendsStore
{
    Task<FriendsStoreResult<FriendRequestResponse>> SendRequestAsync(
        Guid senderUserId,
        Guid receiverUserId,
        CancellationToken cancellationToken = default);

    Task<FriendsStoreResult<FriendResponse>> AcceptRequestAsync(
        Guid actorUserId,
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<FriendsStoreError> DeclineRequestAsync(
        Guid actorUserId,
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<FriendsStoreError> CancelRequestAsync(
        Guid actorUserId,
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<FriendsStoreError> UnfriendAsync(
        Guid actorUserId,
        Guid otherUserId,
        CancellationToken cancellationToken = default);

    Task<FriendsStoreError> BlockAsync(
        Guid actorUserId,
        Guid blockedUserId,
        CancellationToken cancellationToken = default);

    Task<FriendsStoreError> UnblockAsync(
        Guid actorUserId,
        Guid blockedUserId,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<FriendResponse>> GetFriendsAsync(
        Guid userId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<FriendRequestResponse>> GetIncomingRequestsAsync(
        Guid userId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<FriendRequestResponse>> GetOutgoingRequestsAsync(
        Guid userId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<BlockedUserResponse>> GetBlockedUsersAsync(
        Guid userId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    Task<FriendsStoreResult<RelationshipStatusResponse>> GetStatusAsync(
        Guid userId,
        Guid otherUserId,
        CancellationToken cancellationToken = default);

    Task<FriendsStoreResult<MutualFriendsResponse>> GetMutualFriendsAsync(
        Guid userId,
        Guid otherUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);
}
