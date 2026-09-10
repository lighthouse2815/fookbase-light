using Fookbase.Api.Modules.Friends.DTOs.Responses;
using Fookbase.Api.Modules.Friends.Common;
using Fookbase.Api.Persistence;
using Fookbase.Api.Modules.Friends.Entities;
using Fookbase.Api.Modules.Messages.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

namespace Fookbase.Api.Modules.Friends.Services;

public sealed class FriendsService(
    FookbaseDbContext dbContext,
    IHubContext<MessagesHub> hubContext,
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

        return await SendRequestCoreAsync(actorUserId, receiverUserId, cancellationToken);
    }

    public async Task<ApplicationResult<FriendResponse>> AcceptRequestAsync(
        Guid actorUserId,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        await AcceptRequestCoreAsync(actorUserId, requestId, cancellationToken);

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

    public Task<ApplicationResult<PagedResponse<FriendNotificationResponse>>> GetUnreadNotificationsAsync(
        Guid actorUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default) =>
        ReadPageAsync(offset, limit, (normalizedOffset, normalizedLimit) =>
            GetUnreadNotificationsCoreAsync(actorUserId, normalizedOffset, normalizedLimit, cancellationToken));

    public async Task<ApplicationResult> MarkNotificationReadAsync(
        Guid actorUserId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await dbContext.FriendNotifications.SingleOrDefaultAsync(
            item => item.Id == notificationId && item.RecipientUserId == actorUserId,
            cancellationToken);
        if (notification is null)
        {
            return ApplicationResult.Failure(ToApplicationError(FriendsOperationError.NotificationNotFound));
        }

        notification.MarkRead(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult.Success();
    }

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

    public async Task<RelationshipAccessSnapshot> GetAccessSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var friendUserIds = await dbContext.Friendships.AsNoTracking()
            .Where(friendship => friendship.UserId1 == userId || friendship.UserId2 == userId)
            .Select(friendship => friendship.UserId1 == userId
                ? friendship.UserId2
                : friendship.UserId1)
            .ToHashSetAsync(cancellationToken);
        var blockedUserIds = await dbContext.BlockedUsers.AsNoTracking()
            .Where(block => block.BlockerUserId == userId || block.BlockedUserId == userId)
            .Select(block => block.BlockerUserId == userId
                ? block.BlockedUserId
                : block.BlockerUserId)
            .ToHashSetAsync(cancellationToken);

        return new RelationshipAccessSnapshot(friendUserIds, blockedUserIds);
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

    private async Task<ApplicationResult<FriendRequestResponse>> SendRequestCoreAsync(
        Guid senderUserId,
        Guid receiverUserId,
        CancellationToken cancellationToken = default)
    {
        var pair = UserPair.Create(senderUserId, receiverUserId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquirePairLockAsync(pair, cancellationToken);

        if (await IsBlockedAsync(senderUserId, receiverUserId, cancellationToken))
        {
            return Map(await RollbackFailureAsync<FriendRequestResponse>(
                transaction,
                FriendsOperationError.RelationshipBlocked,
                cancellationToken));
        }

        if (await FriendshipExistsAsync(pair, cancellationToken))
        {
            return Map(await RollbackFailureAsync<FriendRequestResponse>(
                transaction,
                FriendsOperationError.AlreadyFriends,
                cancellationToken));
        }

        if (await PendingRequestExistsAsync(pair, cancellationToken))
        {
            return Map(await RollbackFailureAsync<FriendRequestResponse>(
                transaction,
                FriendsOperationError.PendingRequestExists,
                cancellationToken));
        }

        var now = timeProvider.GetUtcNow();
        var request = FriendRequest.Create(Guid.NewGuid(), senderUserId, receiverUserId, now);
        var notification = FriendNotification.Create(
            Guid.NewGuid(),
            receiverUserId,
            senderUserId,
            request.Id,
            FriendNotificationType.FriendRequestReceived,
            now);
        dbContext.FriendRequests.Add(request);
        dbContext.FriendNotifications.Add(notification);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await PublishNotificationAsync(notification, cancellationToken);
        return ApplicationResult<FriendRequestResponse>.Success(ToResponse(request));
    }

    private async Task<ApplicationResult<FriendResponse>> AcceptRequestCoreAsync(
        Guid actorUserId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await dbContext.FriendRequests.AsNoTracking()
            .SingleOrDefaultAsync(request => request.Id == requestId, cancellationToken);
        if (snapshot is null)
        {
            return ApplicationResult<FriendResponse>.Failure(ToApplicationError(FriendsOperationError.RequestNotFound));
        }

        var pair = UserPair.Create(snapshot.UserId1, snapshot.UserId2);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquirePairLockAsync(pair, cancellationToken);
        var request = await dbContext.FriendRequests.SingleAsync(
            item => item.Id == requestId,
            cancellationToken);

        if (actorUserId != request.ReceiverUserId)
        {
            return Map(await RollbackFailureAsync<FriendResponse>(
                transaction,
                FriendsOperationError.Forbidden,
                cancellationToken));
        }

        if (request.Status != FriendRequestStatus.Pending)
        {
            return Map(await RollbackFailureAsync<FriendResponse>(
                transaction,
                FriendsOperationError.RequestNotPending,
                cancellationToken));
        }

        if (await IsBlockedAsync(request.SenderUserId, request.ReceiverUserId, cancellationToken))
        {
            return Map(await RollbackFailureAsync<FriendResponse>(
                transaction,
                FriendsOperationError.RelationshipBlocked,
                cancellationToken));
        }

        if (await FriendshipExistsAsync(pair, cancellationToken))
        {
            return Map(await RollbackFailureAsync<FriendResponse>(
                transaction,
                FriendsOperationError.AlreadyFriends,
                cancellationToken));
        }

        var now = timeProvider.GetUtcNow();
        request.Accept(actorUserId, now);
        var friendship = Friendship.Create(Guid.NewGuid(), pair.UserId1, pair.UserId2, now);
        var notification = FriendNotification.Create(
            Guid.NewGuid(),
            request.SenderUserId,
            actorUserId,
            request.Id,
            FriendNotificationType.FriendRequestAccepted,
            now);
        dbContext.Friendships.Add(friendship);
        dbContext.FriendNotifications.Add(notification);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await PublishNotificationAsync(notification, cancellationToken);
        return ApplicationResult<FriendResponse>.Success(
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

        dbContext.Friendships.Remove(friendship);
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
            dbContext.BlockedUsers.Remove(block);
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

    private async Task<PagedResponse<FriendNotificationResponse>> GetUnreadNotificationsCoreAsync(
        Guid userId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.FriendNotifications.AsNoTracking()
            .Where(notification => notification.RecipientUserId == userId && notification.ReadAtUtc == null);
        var total = await query.CountAsync(cancellationToken);
        var notifications = await query
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .ThenByDescending(notification => notification.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return new PagedResponse<FriendNotificationResponse>(
            notifications.Select(ToResponse).ToList(),
            offset,
            limit,
            total);
    }

    private async Task<FriendsOperationResult<RelationshipStatusResponse>> GetStatusCoreAsync(
        Guid userId,
        Guid otherUserId,
        CancellationToken cancellationToken = default)
    {
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

    private static FriendRequestResponse ToResponse(FriendRequest request) =>
        new(
            request.Id,
            request.SenderUserId,
            request.ReceiverUserId,
            request.Status.ToString().ToLowerInvariant(),
            request.CreatedAtUtc,
            request.RespondedAtUtc);

    private static FriendNotificationResponse ToResponse(FriendNotification notification) =>
        new(
            notification.Id,
            notification.ActorUserId,
            notification.FriendRequestId,
            notification.Type == FriendNotificationType.FriendRequestReceived
                ? "friend_request"
                : "friend_accepted",
            notification.CreatedAtUtc,
            notification.ReadAtUtc);

    private async Task PublishNotificationAsync(
        FriendNotification notification,
        CancellationToken cancellationToken)
    {
        try
        {
            await hubContext.Clients.User(notification.RecipientUserId.ToString()).SendAsync(
                "FriendNotificationReceived",
                ToResponse(notification),
                cancellationToken);
        }
        catch
        {
            // The notification is persisted and will be fetched when the recipient reconnects.
        }
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
        FriendsOperationError.RequestNotFound => new(
            "request_not_found", "The friend request was not found.", ApplicationErrorType.NotFound),
        FriendsOperationError.NotificationNotFound => new(
            "notification_not_found", "The notification was not found.", ApplicationErrorType.NotFound),
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
        RequestNotFound,
        NotificationNotFound,
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

public sealed record RelationshipAccessSnapshot(
    IReadOnlySet<Guid> FriendUserIds,
    IReadOnlySet<Guid> BlockedUserIds);
