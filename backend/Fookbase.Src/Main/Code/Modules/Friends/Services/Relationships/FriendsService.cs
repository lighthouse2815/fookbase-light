using Fookbase.Api.Modules.Friends.Services.Abstractions;
using Fookbase.Api.Modules.Friends.Services.Common;

namespace Fookbase.Api.Modules.Friends.Services.Relationships;

public sealed class FriendsService(IFriendsStore store) : IFriendsService
{
    private const int MaximumLimit = 100;

    public async Task<ApplicationResult<FriendRequestResponse>> SendRequestAsync(
        Guid actorUserId,
        Guid receiverUserId,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == receiverUserId)
        {
            return SelfFailure<FriendRequestResponse>("send a friend request to");
        }

        return Map(await store.SendRequestAsync(actorUserId, receiverUserId, cancellationToken));
    }

    public async Task<ApplicationResult<FriendResponse>> AcceptRequestAsync(
        Guid actorUserId,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        Map(await store.AcceptRequestAsync(actorUserId, requestId, cancellationToken));

    public async Task<ApplicationResult> DeclineRequestAsync(
        Guid actorUserId,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        Map(await store.DeclineRequestAsync(actorUserId, requestId, cancellationToken));

    public async Task<ApplicationResult> CancelRequestAsync(
        Guid actorUserId,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        Map(await store.CancelRequestAsync(actorUserId, requestId, cancellationToken));

    public async Task<ApplicationResult> UnfriendAsync(
        Guid actorUserId,
        Guid otherUserId,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == otherUserId)
        {
            return ApplicationResult.Failure(SelfError("unfriend"));
        }

        return Map(await store.UnfriendAsync(actorUserId, otherUserId, cancellationToken));
    }

    public async Task<ApplicationResult> BlockAsync(
        Guid actorUserId,
        Guid blockedUserId,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == blockedUserId)
        {
            return ApplicationResult.Failure(SelfError("block"));
        }

        return Map(await store.BlockAsync(actorUserId, blockedUserId, cancellationToken));
    }

    public async Task<ApplicationResult> UnblockAsync(
        Guid actorUserId,
        Guid blockedUserId,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == blockedUserId)
        {
            return ApplicationResult.Failure(SelfError("unblock"));
        }

        return Map(await store.UnblockAsync(actorUserId, blockedUserId, cancellationToken));
    }

    public async Task<ApplicationResult<PagedResponse<FriendResponse>>> GetFriendsAsync(
        Guid actorUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default) =>
        await ReadPageAsync(offset, limit, (normalizedOffset, normalizedLimit) =>
            store.GetFriendsAsync(actorUserId, normalizedOffset, normalizedLimit, cancellationToken));

    public async Task<ApplicationResult<PagedResponse<FriendRequestResponse>>> GetIncomingRequestsAsync(
        Guid actorUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default) =>
        await ReadPageAsync(offset, limit, (normalizedOffset, normalizedLimit) =>
            store.GetIncomingRequestsAsync(actorUserId, normalizedOffset, normalizedLimit, cancellationToken));

    public async Task<ApplicationResult<PagedResponse<FriendRequestResponse>>> GetOutgoingRequestsAsync(
        Guid actorUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default) =>
        await ReadPageAsync(offset, limit, (normalizedOffset, normalizedLimit) =>
            store.GetOutgoingRequestsAsync(actorUserId, normalizedOffset, normalizedLimit, cancellationToken));

    public async Task<ApplicationResult<PagedResponse<BlockedUserResponse>>> GetBlockedUsersAsync(
        Guid actorUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default) =>
        await ReadPageAsync(offset, limit, (normalizedOffset, normalizedLimit) =>
            store.GetBlockedUsersAsync(actorUserId, normalizedOffset, normalizedLimit, cancellationToken));

    public async Task<ApplicationResult<RelationshipStatusResponse>> GetStatusAsync(
        Guid actorUserId,
        Guid otherUserId,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == otherUserId)
        {
            return ApplicationResult<RelationshipStatusResponse>.Success(
                new RelationshipStatusResponse(otherUserId, "self"));
        }

        return Map(await store.GetStatusAsync(actorUserId, otherUserId, cancellationToken));
    }

    public async Task<ApplicationResult<MutualFriendsResponse>> GetMutualFriendsAsync(
        Guid actorUserId,
        Guid otherUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == otherUserId)
        {
            return SelfFailure<MutualFriendsResponse>("query mutual friends with");
        }

        var paginationError = ValidatePagination(offset, limit);
        if (paginationError is not null)
        {
            return ApplicationResult<MutualFriendsResponse>.Failure(paginationError);
        }

        return Map(await store.GetMutualFriendsAsync(
            actorUserId,
            otherUserId,
            offset,
            limit,
            cancellationToken));
    }

    private static async Task<ApplicationResult<PagedResponse<T>>> ReadPageAsync<T>(
        int offset,
        int limit,
        Func<int, int, Task<PagedResponse<T>>> reader)
    {
        var paginationError = ValidatePagination(offset, limit);
        if (paginationError is not null)
        {
            return ApplicationResult<PagedResponse<T>>.Failure(paginationError);
        }

        return ApplicationResult<PagedResponse<T>>.Success(
            await reader(offset, limit));
    }

    private static ApplicationError? ValidatePagination(int offset, int limit)
    {
        if (offset < 0 || limit < 1 || limit > MaximumLimit)
        {
            return new ApplicationError(
                "invalid_pagination",
                $"Offset must be non-negative and limit must be between 1 and {MaximumLimit}.",
                ApplicationErrorType.Validation);
        }

        return null;
    }

    private static ApplicationResult<T> Map<T>(FriendsStoreResult<T> result) =>
        result.Succeeded
            ? ApplicationResult<T>.Success(result.Value!)
            : ApplicationResult<T>.Failure(ToApplicationError(result.Error));

    private static ApplicationResult Map(FriendsStoreError error) =>
        error == FriendsStoreError.None
            ? ApplicationResult.Success()
            : ApplicationResult.Failure(ToApplicationError(error));

    private static ApplicationResult<T> SelfFailure<T>(string operation) =>
        ApplicationResult<T>.Failure(SelfError(operation));

    private static ApplicationError SelfError(string operation) =>
        new(
            "self_relationship_not_allowed",
            $"A user cannot {operation} themselves.",
            ApplicationErrorType.Validation);

    private static ApplicationError ToApplicationError(FriendsStoreError error) => error switch
    {
        FriendsStoreError.UserNotFound => new(
            "user_not_found", "The target user was not found.", ApplicationErrorType.NotFound),
        FriendsStoreError.RequestNotFound => new(
            "request_not_found", "The friend request was not found.", ApplicationErrorType.NotFound),
        FriendsStoreError.FriendshipNotFound => new(
            "friendship_not_found", "The friendship was not found.", ApplicationErrorType.NotFound),
        FriendsStoreError.Forbidden => new(
            "forbidden", "You are not allowed to perform this operation.", ApplicationErrorType.Forbidden),
        FriendsStoreError.AlreadyFriends => new(
            "already_friends", "The users are already friends.", ApplicationErrorType.Conflict),
        FriendsStoreError.PendingRequestExists => new(
            "pending_request_exists", "A pending friend request already exists.", ApplicationErrorType.Conflict),
        FriendsStoreError.RelationshipBlocked => new(
            "relationship_unavailable", "This relationship operation is unavailable.", ApplicationErrorType.Conflict),
        FriendsStoreError.RequestNotPending => new(
            "request_not_pending", "The friend request is no longer pending.", ApplicationErrorType.Conflict),
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null)
    };
}
