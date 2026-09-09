using Fookbase.Api.Modules.Friends.DTOs.Responses;
using Fookbase.Api.Modules.Friends.Common;
using System.Text.Json;
using Fookbase.Api.Shared.Contracts.Friends;
using Fookbase.Api.Modules.Friends.Data;
using Fookbase.Api.Modules.Friends.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Friends.Services;

public sealed class FriendsService(
    FriendsDbContext dbContext,
    TimeProvider timeProvider)
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

        return Map(await SendRequestCoreAsync(actorUserId, receiverUserId, cancellationToken));
    }

    public async Task<ApplicationResult<FriendResponse>> AcceptRequestAsync(
        Guid actorUserId,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        Map(await AcceptRequestCoreAsync(actorUserId, requestId, cancellationToken));

    public async Task<ApplicationResult> DeclineRequestAsync(
        Guid actorUserId,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        Map(await DeclineRequestCoreAsync(actorUserId, requestId, cancellationToken));

    public async Task<ApplicationResult> CancelRequestAsync(
        Guid actorUserId,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        Map(await CancelRequestCoreAsync(actorUserId, requestId, cancellationToken));

    public async Task<ApplicationResult> UnfriendAsync(
        Guid actorUserId,
        Guid otherUserId,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == otherUserId)
        {
            return ApplicationResult.Failure(SelfError("unfriend"));
        }

        return Map(await UnfriendCoreAsync(actorUserId, otherUserId, cancellationToken));
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

        return Map(await BlockCoreAsync(actorUserId, blockedUserId, cancellationToken));
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

        return Map(await UnblockCoreAsync(actorUserId, blockedUserId, cancellationToken));
    }

    public Task<ApplicationResult<PagedResponse<FriendResponse>>> GetFriendsAsync(
        Guid actorUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default) =>
        ReadPageAsync(offset, limit, (normalizedOffset, normalizedLimit) =>
            GetFriendsCoreAsync(actorUserId, normalizedOffset, normalizedLimit, cancellationToken));

    public Task<ApplicationResult<PagedResponse<FriendRequestResponse>>> GetIncomingRequestsAsync(
        Guid actorUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default) =>
        ReadPageAsync(offset, limit, (normalizedOffset, normalizedLimit) =>
            GetIncomingRequestsCoreAsync(actorUserId, normalizedOffset, normalizedLimit, cancellationToken));

    public Task<ApplicationResult<PagedResponse<FriendRequestResponse>>> GetOutgoingRequestsAsync(
        Guid actorUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default) =>
        ReadPageAsync(offset, limit, (normalizedOffset, normalizedLimit) =>
            GetOutgoingRequestsCoreAsync(actorUserId, normalizedOffset, normalizedLimit, cancellationToken));

    public Task<ApplicationResult<PagedResponse<BlockedUserResponse>>> GetBlockedUsersAsync(
        Guid actorUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default) =>
        ReadPageAsync(offset, limit, (normalizedOffset, normalizedLimit) =>
            GetBlockedUsersCoreAsync(actorUserId, normalizedOffset, normalizedLimit, cancellationToken));

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

        return Map(await GetStatusCoreAsync(actorUserId, otherUserId, cancellationToken));
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

        return Map(await GetMutualFriendsCoreAsync(
            actorUserId,
            otherUserId,
            offset,
            limit,
            cancellationToken));
    }

    private async Task<FriendsOperationResult<FriendRequestResponse>> SendRequestCoreAsync(
        Guid senderUserId,
        Guid receiverUserId,
        CancellationToken cancellationToken = default)
    {
        var pair = UserPair.Create(senderUserId, receiverUserId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquirePairLockAsync(pair, cancellationToken);

        if (!await UsersExistAsync(senderUserId, receiverUserId, cancellationToken))
        {
            return await RollbackFailureAsync<FriendRequestResponse>(
                transaction,
                FriendsOperationError.UserNotFound,
                cancellationToken);
        }

        if (await IsBlockedAsync(senderUserId, receiverUserId, cancellationToken))
        {
            return await RollbackFailureAsync<FriendRequestResponse>(
                transaction,
                FriendsOperationError.RelationshipBlocked,
                cancellationToken);
        }

        if (await FriendshipExistsAsync(pair, cancellationToken))
        {
            return await RollbackFailureAsync<FriendRequestResponse>(
                transaction,
                FriendsOperationError.AlreadyFriends,
                cancellationToken);
        }

        if (await PendingRequestExistsAsync(pair, cancellationToken))
        {
            return await RollbackFailureAsync<FriendRequestResponse>(
                transaction,
                FriendsOperationError.PendingRequestExists,
                cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        var request = FriendRequest.Create(Guid.NewGuid(), senderUserId, receiverUserId, now);
        var integrationEvent = new FriendRequestSentIntegrationEvent(
            Guid.NewGuid(), request.Id, senderUserId, receiverUserId, now);
        dbContext.FriendRequests.Add(request);
        AddOutbox(integrationEvent.EventId, FriendRequestSentIntegrationEvent.EventType, integrationEvent, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return FriendsOperationResult<FriendRequestResponse>.Success(ToResponse(request));
    }

    private async Task<FriendsOperationResult<FriendResponse>> AcceptRequestCoreAsync(
        Guid actorUserId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await dbContext.FriendRequests.AsNoTracking()
            .SingleOrDefaultAsync(request => request.Id == requestId, cancellationToken);
        if (snapshot is null)
        {
            return FriendsOperationResult<FriendResponse>.Failure(FriendsOperationError.RequestNotFound);
        }

        var pair = UserPair.Create(snapshot.UserId1, snapshot.UserId2);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquirePairLockAsync(pair, cancellationToken);
        var request = await dbContext.FriendRequests.SingleAsync(
            item => item.Id == requestId,
            cancellationToken);

        if (actorUserId != request.ReceiverUserId)
        {
            return await RollbackFailureAsync<FriendResponse>(
                transaction,
                FriendsOperationError.Forbidden,
                cancellationToken);
        }

        if (request.Status != FriendRequestStatus.Pending)
        {
            return await RollbackFailureAsync<FriendResponse>(
                transaction,
                FriendsOperationError.RequestNotPending,
                cancellationToken);
        }

        if (await IsBlockedAsync(request.SenderUserId, request.ReceiverUserId, cancellationToken))
        {
            return await RollbackFailureAsync<FriendResponse>(
                transaction,
                FriendsOperationError.RelationshipBlocked,
                cancellationToken);
        }

        if (await FriendshipExistsAsync(pair, cancellationToken))
        {
            return await RollbackFailureAsync<FriendResponse>(
                transaction,
                FriendsOperationError.AlreadyFriends,
                cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        request.Accept(actorUserId, now);
        var friendship = Friendship.Create(Guid.NewGuid(), pair.UserId1, pair.UserId2, now);
        var integrationEvent = new FriendRequestAcceptedIntegrationEvent(
            Guid.NewGuid(), request.Id, pair.UserId1, pair.UserId2, now);
        dbContext.Friendships.Add(friendship);
        AddOutbox(
            integrationEvent.EventId,
            FriendRequestAcceptedIntegrationEvent.EventType,
            integrationEvent,
            now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return FriendsOperationResult<FriendResponse>.Success(
            new FriendResponse(friendship.OtherUserId(actorUserId), friendship.CreatedAtUtc));
    }

    private Task<FriendsOperationError> DeclineRequestCoreAsync(
        Guid actorUserId,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        ChangeRequestStatusAsync(
            actorUserId,
            requestId,
            requireReceiver: true,
            (request, now) => request.Decline(actorUserId, now),
            cancellationToken);

    private Task<FriendsOperationError> CancelRequestCoreAsync(
        Guid actorUserId,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        ChangeRequestStatusAsync(
            actorUserId,
            requestId,
            requireReceiver: false,
            (request, now) => request.Cancel(actorUserId, now),
            cancellationToken);

    private async Task<FriendsOperationError> UnfriendCoreAsync(
        Guid actorUserId,
        Guid otherUserId,
        CancellationToken cancellationToken = default)
    {
        var pair = UserPair.Create(actorUserId, otherUserId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquirePairLockAsync(pair, cancellationToken);
        var friendship = await dbContext.Friendships.SingleOrDefaultAsync(
            item => item.UserId1 == pair.UserId1 && item.UserId2 == pair.UserId2,
            cancellationToken);
        if (friendship is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return FriendsOperationError.FriendshipNotFound;
        }

        if (!friendship.Contains(actorUserId))
        {
            await transaction.RollbackAsync(cancellationToken);
            return FriendsOperationError.Forbidden;
        }

        var now = timeProvider.GetUtcNow();
        var integrationEvent = new FriendshipRemovedIntegrationEvent(
            Guid.NewGuid(), pair.UserId1, pair.UserId2, now);
        dbContext.Friendships.Remove(friendship);
        AddOutbox(
            integrationEvent.EventId,
            FriendshipRemovedIntegrationEvent.EventType,
            integrationEvent,
            now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return FriendsOperationError.None;
    }

    private async Task<FriendsOperationError> BlockCoreAsync(
        Guid actorUserId,
        Guid blockedUserId,
        CancellationToken cancellationToken = default)
    {
        var pair = UserPair.Create(actorUserId, blockedUserId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquirePairLockAsync(pair, cancellationToken);

        if (!await UsersExistAsync(actorUserId, blockedUserId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return FriendsOperationError.UserNotFound;
        }

        if (await dbContext.BlockedUsers.AnyAsync(
                block => block.BlockerUserId == actorUserId && block.BlockedUserId == blockedUserId,
                cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return FriendsOperationError.None;
        }

        var now = timeProvider.GetUtcNow();
        dbContext.BlockedUsers.Add(BlockedUser.Create(actorUserId, blockedUserId, now));

        var friendship = await dbContext.Friendships.SingleOrDefaultAsync(
            item => item.UserId1 == pair.UserId1 && item.UserId2 == pair.UserId2,
            cancellationToken);
        if (friendship is not null)
        {
            dbContext.Friendships.Remove(friendship);
            var removedEvent = new FriendshipRemovedIntegrationEvent(
                Guid.NewGuid(), pair.UserId1, pair.UserId2, now);
            AddOutbox(
                removedEvent.EventId,
                FriendshipRemovedIntegrationEvent.EventType,
                removedEvent,
                now);
        }

        var pendingRequests = await dbContext.FriendRequests
            .Where(request =>
                request.UserId1 == pair.UserId1 &&
                request.UserId2 == pair.UserId2 &&
                request.Status == FriendRequestStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var pendingRequest in pendingRequests)
        {
            pendingRequest.CancelBecauseBlocked(now);
        }

        var blockedEvent = new UserBlockedIntegrationEvent(
            Guid.NewGuid(), actorUserId, blockedUserId, now);
        AddOutbox(
            blockedEvent.EventId,
            UserBlockedIntegrationEvent.EventType,
            blockedEvent,
            now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return FriendsOperationError.None;
    }

    private async Task<FriendsOperationError> UnblockCoreAsync(
        Guid actorUserId,
        Guid blockedUserId,
        CancellationToken cancellationToken = default)
    {
        var pair = UserPair.Create(actorUserId, blockedUserId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquirePairLockAsync(pair, cancellationToken);
        var block = await dbContext.BlockedUsers.SingleOrDefaultAsync(
            item => item.BlockerUserId == actorUserId && item.BlockedUserId == blockedUserId,
            cancellationToken);
        if (block is not null)
        {
            var now = timeProvider.GetUtcNow();
            var integrationEvent = new UserUnblockedIntegrationEvent(
                Guid.NewGuid(), actorUserId, blockedUserId, now);
            dbContext.BlockedUsers.Remove(block);
            AddOutbox(
                integrationEvent.EventId,
                UserUnblockedIntegrationEvent.EventType,
                integrationEvent,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return FriendsOperationError.None;
    }

    private async Task<PagedResponse<FriendResponse>> GetFriendsCoreAsync(
        Guid userId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Friendships.AsNoTracking()
            .Where(friendship => friendship.UserId1 == userId || friendship.UserId2 == userId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(friendship => friendship.UserId1 == userId
                ? friendship.UserId2
                : friendship.UserId1)
            .Skip(offset)
            .Take(limit)
            .Select(friendship => new FriendResponse(
                friendship.UserId1 == userId ? friendship.UserId2 : friendship.UserId1,
                friendship.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return new PagedResponse<FriendResponse>(items, offset, limit, total);
    }

    private Task<PagedResponse<FriendRequestResponse>> GetIncomingRequestsCoreAsync(
        Guid userId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default) =>
        GetRequestsAsync(
            incoming: true,
            userId,
            offset,
            limit,
            cancellationToken);

    private Task<PagedResponse<FriendRequestResponse>> GetOutgoingRequestsCoreAsync(
        Guid userId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default) =>
        GetRequestsAsync(
            incoming: false,
            userId,
            offset,
            limit,
            cancellationToken);

    private async Task<PagedResponse<BlockedUserResponse>> GetBlockedUsersCoreAsync(
        Guid userId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.BlockedUsers.AsNoTracking()
            .Where(block => block.BlockerUserId == userId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(block => block.CreatedAtUtc)
            .ThenBy(block => block.BlockedUserId)
            .Skip(offset)
            .Take(limit)
            .Select(block => new BlockedUserResponse(block.BlockedUserId, block.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return new PagedResponse<BlockedUserResponse>(items, offset, limit, total);
    }

    private async Task<FriendsOperationResult<RelationshipStatusResponse>> GetStatusCoreAsync(
        Guid userId,
        Guid otherUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.KnownUsers.AnyAsync(user => user.UserId == otherUserId, cancellationToken))
        {
            return FriendsOperationResult<RelationshipStatusResponse>.Failure(
                FriendsOperationError.UserNotFound);
        }

        var pair = UserPair.Create(userId, otherUserId);
        if (await IsBlockedAsync(userId, otherUserId, cancellationToken))
        {
            return FriendsOperationResult<RelationshipStatusResponse>.Success(
                new RelationshipStatusResponse(otherUserId, "blocked"));
        }

        if (await FriendshipExistsAsync(pair, cancellationToken))
        {
            return FriendsOperationResult<RelationshipStatusResponse>.Success(
                new RelationshipStatusResponse(otherUserId, "friends"));
        }

        var pendingRequest = await dbContext.FriendRequests.AsNoTracking()
            .SingleOrDefaultAsync(
                request =>
                    request.UserId1 == pair.UserId1 &&
                    request.UserId2 == pair.UserId2 &&
                    request.Status == FriendRequestStatus.Pending,
                cancellationToken);
        if (pendingRequest is null)
        {
            return FriendsOperationResult<RelationshipStatusResponse>.Success(
                new RelationshipStatusResponse(otherUserId, "none"));
        }

        var status = pendingRequest.SenderUserId == userId
            ? "request_sent"
            : "request_received";
        return FriendsOperationResult<RelationshipStatusResponse>.Success(
            new RelationshipStatusResponse(otherUserId, status, pendingRequest.Id));
    }

    private async Task<FriendsOperationResult<MutualFriendsResponse>> GetMutualFriendsCoreAsync(
        Guid userId,
        Guid otherUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.KnownUsers.AnyAsync(user => user.UserId == otherUserId, cancellationToken))
        {
            return FriendsOperationResult<MutualFriendsResponse>.Failure(FriendsOperationError.UserNotFound);
        }

        var firstFriends = FriendIds(userId);
        var secondFriends = FriendIds(otherUserId);
        var mutual = firstFriends.Intersect(secondFriends);
        var total = await mutual.CountAsync(cancellationToken);
        var userIds = await mutual.OrderBy(id => id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return FriendsOperationResult<MutualFriendsResponse>.Success(
            new MutualFriendsResponse(total, userIds, offset, limit));
    }

    private async Task<FriendsOperationError> ChangeRequestStatusAsync(
        Guid actorUserId,
        Guid requestId,
        bool requireReceiver,
        Action<FriendRequest, DateTimeOffset> change,
        CancellationToken cancellationToken)
    {
        var snapshot = await dbContext.FriendRequests.AsNoTracking()
            .SingleOrDefaultAsync(request => request.Id == requestId, cancellationToken);
        if (snapshot is null)
        {
            return FriendsOperationError.RequestNotFound;
        }

        var pair = UserPair.Create(snapshot.UserId1, snapshot.UserId2);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquirePairLockAsync(pair, cancellationToken);
        var request = await dbContext.FriendRequests.SingleAsync(
            item => item.Id == requestId,
            cancellationToken);
        var authorizedUserId = requireReceiver ? request.ReceiverUserId : request.SenderUserId;
        if (actorUserId != authorizedUserId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return FriendsOperationError.Forbidden;
        }

        if (request.Status != FriendRequestStatus.Pending)
        {
            await transaction.RollbackAsync(cancellationToken);
            return FriendsOperationError.RequestNotPending;
        }

        change(request, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return FriendsOperationError.None;
    }

    private async Task<PagedResponse<FriendRequestResponse>> GetRequestsAsync(
        bool incoming,
        Guid userId,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = dbContext.FriendRequests.AsNoTracking()
            .Where(request =>
                request.Status == FriendRequestStatus.Pending &&
                (incoming
                    ? request.ReceiverUserId == userId
                    : request.SenderUserId == userId));
        var total = await query.CountAsync(cancellationToken);
        var requests = await query.OrderByDescending(request => request.CreatedAtUtc)
            .ThenBy(request => request.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return new PagedResponse<FriendRequestResponse>(
            requests.Select(ToResponse).ToList(),
            offset,
            limit,
            total);
    }

    private IQueryable<Guid> FriendIds(Guid userId) =>
        dbContext.Friendships.AsNoTracking()
            .Where(friendship => friendship.UserId1 == userId || friendship.UserId2 == userId)
            .Select(friendship => friendship.UserId1 == userId
                ? friendship.UserId2
                : friendship.UserId1);

    private async Task<bool> UsersExistAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken) =>
        await dbContext.KnownUsers.CountAsync(
            user => user.UserId == firstUserId || user.UserId == secondUserId,
            cancellationToken) == 2;

    private Task<bool> IsBlockedAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken) =>
        dbContext.BlockedUsers.AnyAsync(
            block =>
                (block.BlockerUserId == firstUserId && block.BlockedUserId == secondUserId) ||
                (block.BlockerUserId == secondUserId && block.BlockedUserId == firstUserId),
            cancellationToken);

    private Task<bool> FriendshipExistsAsync(UserPair pair, CancellationToken cancellationToken) =>
        dbContext.Friendships.AnyAsync(
            friendship =>
                friendship.UserId1 == pair.UserId1 && friendship.UserId2 == pair.UserId2,
            cancellationToken);

    private Task<bool> PendingRequestExistsAsync(UserPair pair, CancellationToken cancellationToken) =>
        dbContext.FriendRequests.AnyAsync(
            request =>
                request.UserId1 == pair.UserId1 &&
                request.UserId2 == pair.UserId2 &&
                request.Status == FriendRequestStatus.Pending,
            cancellationToken);

    private Task<int> AcquirePairLockAsync(UserPair pair, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({PairLockKey(pair)}, 0))",
            cancellationToken);

    private static string PairLockKey(UserPair pair) =>
        $"{pair.UserId1:N}:{pair.UserId2:N}";

    private void AddOutbox(
        Guid eventId,
        string eventType,
        object integrationEvent,
        DateTimeOffset occurredAtUtc) =>
        dbContext.OutboxMessages.Add(
            OutboxMessage.Create(
                eventId,
                eventType,
                JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
                occurredAtUtc));

    private static FriendRequestResponse ToResponse(FriendRequest request) =>
        new(
            request.Id,
            request.SenderUserId,
            request.ReceiverUserId,
            request.Status.ToString().ToLowerInvariant(),
            request.CreatedAtUtc,
            request.RespondedAtUtc);

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

        return ApplicationResult<PagedResponse<T>>.Success(await reader(offset, limit));
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

    private static ApplicationResult<T> Map<T>(FriendsOperationResult<T> result) =>
        result.Succeeded
            ? ApplicationResult<T>.Success(result.Value!)
            : ApplicationResult<T>.Failure(ToApplicationError(result.Error));

    private static ApplicationResult Map(FriendsOperationError error) =>
        error == FriendsOperationError.None
            ? ApplicationResult.Success()
            : ApplicationResult.Failure(ToApplicationError(error));

    private static ApplicationResult<T> SelfFailure<T>(string operation) =>
        ApplicationResult<T>.Failure(SelfError(operation));

    private static ApplicationError SelfError(string operation) =>
        new(
            "self_relationship_not_allowed",
            $"A user cannot {operation} themselves.",
            ApplicationErrorType.Validation);

    private static ApplicationError ToApplicationError(FriendsOperationError error) => error switch
    {
        FriendsOperationError.UserNotFound => new(
            "user_not_found", "The target user was not found.", ApplicationErrorType.NotFound),
        FriendsOperationError.RequestNotFound => new(
            "request_not_found", "The friend request was not found.", ApplicationErrorType.NotFound),
        FriendsOperationError.FriendshipNotFound => new(
            "friendship_not_found", "The friendship was not found.", ApplicationErrorType.NotFound),
        FriendsOperationError.Forbidden => new(
            "forbidden", "You are not allowed to perform this operation.", ApplicationErrorType.Forbidden),
        FriendsOperationError.AlreadyFriends => new(
            "already_friends", "The users are already friends.", ApplicationErrorType.Conflict),
        FriendsOperationError.PendingRequestExists => new(
            "pending_request_exists", "A pending friend request already exists.", ApplicationErrorType.Conflict),
        FriendsOperationError.RelationshipBlocked => new(
            "relationship_unavailable", "This relationship operation is unavailable.", ApplicationErrorType.Conflict),
        FriendsOperationError.RequestNotPending => new(
            "request_not_pending", "The friend request is no longer pending.", ApplicationErrorType.Conflict),
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null)
    };

    private static async Task<FriendsOperationResult<T>> RollbackFailureAsync<T>(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        FriendsOperationError error,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        return FriendsOperationResult<T>.Failure(error);
    }

    private enum FriendsOperationError
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

    private sealed record FriendsOperationResult<T>(T? Value, FriendsOperationError Error)
    {
        public bool Succeeded => Error == FriendsOperationError.None;

        public static FriendsOperationResult<T> Success(T value) =>
            new(value, FriendsOperationError.None);

        public static FriendsOperationResult<T> Failure(FriendsOperationError error) =>
            new(default, error);
    }
}
