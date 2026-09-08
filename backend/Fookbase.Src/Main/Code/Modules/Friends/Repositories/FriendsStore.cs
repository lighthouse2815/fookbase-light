using Fookbase.Api.Modules.Friends.DTOs;
using System.Text.Json;
using Fookbase.Api.Shared.Contracts.Friends;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Friends.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Friends.Repositories;

internal sealed class FriendsStore(
    FriendsDbContext dbContext,
    TimeProvider timeProvider) : IFriendsStore
{
    public async Task<FriendsStoreResult<FriendRequestResponse>> SendRequestAsync(
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
                FriendsStoreError.UserNotFound,
                cancellationToken);
        }

        if (await IsBlockedAsync(senderUserId, receiverUserId, cancellationToken))
        {
            return await RollbackFailureAsync<FriendRequestResponse>(
                transaction,
                FriendsStoreError.RelationshipBlocked,
                cancellationToken);
        }

        if (await FriendshipExistsAsync(pair, cancellationToken))
        {
            return await RollbackFailureAsync<FriendRequestResponse>(
                transaction,
                FriendsStoreError.AlreadyFriends,
                cancellationToken);
        }

        if (await PendingRequestExistsAsync(pair, cancellationToken))
        {
            return await RollbackFailureAsync<FriendRequestResponse>(
                transaction,
                FriendsStoreError.PendingRequestExists,
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
        return FriendsStoreResult<FriendRequestResponse>.Success(ToResponse(request));
    }

    public async Task<FriendsStoreResult<FriendResponse>> AcceptRequestAsync(
        Guid actorUserId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await dbContext.FriendRequests.AsNoTracking()
            .SingleOrDefaultAsync(request => request.Id == requestId, cancellationToken);
        if (snapshot is null)
        {
            return FriendsStoreResult<FriendResponse>.Failure(FriendsStoreError.RequestNotFound);
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
                FriendsStoreError.Forbidden,
                cancellationToken);
        }

        if (request.Status != FriendRequestStatus.Pending)
        {
            return await RollbackFailureAsync<FriendResponse>(
                transaction,
                FriendsStoreError.RequestNotPending,
                cancellationToken);
        }

        if (await IsBlockedAsync(request.SenderUserId, request.ReceiverUserId, cancellationToken))
        {
            return await RollbackFailureAsync<FriendResponse>(
                transaction,
                FriendsStoreError.RelationshipBlocked,
                cancellationToken);
        }

        if (await FriendshipExistsAsync(pair, cancellationToken))
        {
            return await RollbackFailureAsync<FriendResponse>(
                transaction,
                FriendsStoreError.AlreadyFriends,
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
        return FriendsStoreResult<FriendResponse>.Success(
            new FriendResponse(friendship.OtherUserId(actorUserId), friendship.CreatedAtUtc));
    }

    public Task<FriendsStoreError> DeclineRequestAsync(
        Guid actorUserId,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        ChangeRequestStatusAsync(
            actorUserId,
            requestId,
            requireReceiver: true,
            (request, now) => request.Decline(actorUserId, now),
            cancellationToken);

    public Task<FriendsStoreError> CancelRequestAsync(
        Guid actorUserId,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        ChangeRequestStatusAsync(
            actorUserId,
            requestId,
            requireReceiver: false,
            (request, now) => request.Cancel(actorUserId, now),
            cancellationToken);

    public async Task<FriendsStoreError> UnfriendAsync(
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
            return FriendsStoreError.FriendshipNotFound;
        }

        if (!friendship.Contains(actorUserId))
        {
            await transaction.RollbackAsync(cancellationToken);
            return FriendsStoreError.Forbidden;
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
        return FriendsStoreError.None;
    }

    public async Task<FriendsStoreError> BlockAsync(
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
            return FriendsStoreError.UserNotFound;
        }

        if (await dbContext.BlockedUsers.AnyAsync(
                block => block.BlockerUserId == actorUserId && block.BlockedUserId == blockedUserId,
                cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return FriendsStoreError.None;
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
        return FriendsStoreError.None;
    }

    public async Task<FriendsStoreError> UnblockAsync(
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
        return FriendsStoreError.None;
    }

    public async Task<PagedResponse<FriendResponse>> GetFriendsAsync(
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

    public Task<PagedResponse<FriendRequestResponse>> GetIncomingRequestsAsync(
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

    public Task<PagedResponse<FriendRequestResponse>> GetOutgoingRequestsAsync(
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

    public async Task<PagedResponse<BlockedUserResponse>> GetBlockedUsersAsync(
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

    public async Task<FriendsStoreResult<RelationshipStatusResponse>> GetStatusAsync(
        Guid userId,
        Guid otherUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.KnownUsers.AnyAsync(user => user.UserId == otherUserId, cancellationToken))
        {
            return FriendsStoreResult<RelationshipStatusResponse>.Failure(
                FriendsStoreError.UserNotFound);
        }

        var pair = UserPair.Create(userId, otherUserId);
        if (await IsBlockedAsync(userId, otherUserId, cancellationToken))
        {
            return FriendsStoreResult<RelationshipStatusResponse>.Success(
                new RelationshipStatusResponse(otherUserId, "blocked"));
        }

        if (await FriendshipExistsAsync(pair, cancellationToken))
        {
            return FriendsStoreResult<RelationshipStatusResponse>.Success(
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
            return FriendsStoreResult<RelationshipStatusResponse>.Success(
                new RelationshipStatusResponse(otherUserId, "none"));
        }

        var status = pendingRequest.SenderUserId == userId
            ? "request_sent"
            : "request_received";
        return FriendsStoreResult<RelationshipStatusResponse>.Success(
            new RelationshipStatusResponse(otherUserId, status, pendingRequest.Id));
    }

    public async Task<FriendsStoreResult<MutualFriendsResponse>> GetMutualFriendsAsync(
        Guid userId,
        Guid otherUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.KnownUsers.AnyAsync(user => user.UserId == otherUserId, cancellationToken))
        {
            return FriendsStoreResult<MutualFriendsResponse>.Failure(FriendsStoreError.UserNotFound);
        }

        var firstFriends = FriendIds(userId);
        var secondFriends = FriendIds(otherUserId);
        var mutual = firstFriends.Intersect(secondFriends);
        var total = await mutual.CountAsync(cancellationToken);
        var userIds = await mutual.OrderBy(id => id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return FriendsStoreResult<MutualFriendsResponse>.Success(
            new MutualFriendsResponse(total, userIds, offset, limit));
    }

    private async Task<FriendsStoreError> ChangeRequestStatusAsync(
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
            return FriendsStoreError.RequestNotFound;
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
            return FriendsStoreError.Forbidden;
        }

        if (request.Status != FriendRequestStatus.Pending)
        {
            await transaction.RollbackAsync(cancellationToken);
            return FriendsStoreError.RequestNotPending;
        }

        change(request, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return FriendsStoreError.None;
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

    private static async Task<FriendsStoreResult<T>> RollbackFailureAsync<T>(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        FriendsStoreError error,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        return FriendsStoreResult<T>.Failure(error);
    }
}
