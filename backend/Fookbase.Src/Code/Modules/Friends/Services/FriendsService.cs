using Fookbase.Api.Modules.Friends.DTOs.Responses;
using Fookbase.Api.Modules.Friends.Common;
using Fookbase.Api.Persistence;
using Fookbase.Api.Modules.Friends.Entities;
using Fookbase.Api.Modules.Messages.Hubs;
using Fookbase.Api.Modules.Notifications.Entities;
using Fookbase.Api.Modules.Notifications.Services;
using Fookbase.Api.Modules.Users.Entities;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using System.Security.Cryptography;
using System.Text.Json;

namespace Fookbase.Api.Modules.Friends.Services;

public sealed class FriendsService(
    FookbaseDbContext dbContext,
    IHubContext<MessagesHub> hubContext,
    NotificationService notificationService,
    IDataProtectionProvider protectionProvider,
    TimeProvider timeProvider)
{
    private const int MaximumLimit = 100;
    public const int DefaultFollowPageSize = 20;
    public const int MaximumFollowPageSize = 100;
    private const int FollowCursorVersion = 1;

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

    public async Task<ApplicationResult> FollowAsync(
        Guid actorUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == targetUserId)
        {
            return ApplicationResult.Failure(SelfError("follow"));
        }

        return Map(await FollowCoreAsync(actorUserId, targetUserId, cancellationToken));
    }

    public async Task<ApplicationResult> UnfollowAsync(
        Guid actorUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == targetUserId)
        {
            return ApplicationResult.Failure(SelfError("unfollow"));
        }

        return Map(await UnfollowCoreAsync(actorUserId, targetUserId, cancellationToken));
    }

    public Task<ApplicationResult<CursorPageResponse<UserFollowResponse>>> GetFollowersAsync(
        Guid viewerUserId,
        Guid targetUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default) =>
        GetFollowPageAsync(viewerUserId, targetUserId, "followers", cursor, limit, cancellationToken);

    public Task<ApplicationResult<CursorPageResponse<UserFollowResponse>>> GetFollowingAsync(
        Guid viewerUserId,
        Guid targetUserId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default) =>
        GetFollowPageAsync(viewerUserId, targetUserId, "following", cursor, limit, cancellationToken);

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

    public async Task<ApplicationResult<PagedResponse<FriendResponse>>> GetVisibleFriendsAsync(
        Guid viewerUserId,
        Guid targetUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var paginationError = ValidatePagination(offset, limit);
        if (paginationError is not null)
        {
            return ApplicationResult<PagedResponse<FriendResponse>>.Failure(paginationError);
        }

        if (!await CanViewRelationshipListAsync(
                viewerUserId, targetUserId, settings => settings.FriendListVisibility, cancellationToken))
        {
            return ApplicationResult<PagedResponse<FriendResponse>>.Failure(
                ToApplicationError(FriendsOperationError.UserNotFound));
        }

        return ApplicationResult<PagedResponse<FriendResponse>>.Success(
            await GetFriendsCoreAsync(targetUserId, offset, limit, cancellationToken));
    }

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

    public async Task<IReadOnlyDictionary<Guid, RelationshipStatusResponse>> GetStatusesAsync(
        Guid actorUserId,
        IReadOnlyCollection<Guid> otherUserIds,
        CancellationToken cancellationToken = default)
    {
        var userIds = otherUserIds.Distinct().ToArray();
        var statuses = userIds.ToDictionary(
            userId => userId,
            userId => new RelationshipStatusResponse(
                userId,
                userId == actorUserId ? "self" : "none"));
        var relationshipUserIds = userIds.Where(userId => userId != actorUserId).ToArray();
        if (relationshipUserIds.Length == 0)
        {
            return statuses;
        }

        var pendingRequests = await dbContext.FriendRequests.AsNoTracking()
            .Where(request =>
                request.Status == FriendRequestStatus.Pending &&
                ((request.SenderUserId == actorUserId && relationshipUserIds.Contains(request.ReceiverUserId)) ||
                 (request.ReceiverUserId == actorUserId && relationshipUserIds.Contains(request.SenderUserId))))
            .Select(request => new { request.Id, request.SenderUserId, request.ReceiverUserId })
            .ToListAsync(cancellationToken);
        foreach (var request in pendingRequests)
        {
            var otherUserId = request.SenderUserId == actorUserId
                ? request.ReceiverUserId
                : request.SenderUserId;
            statuses[otherUserId] = new RelationshipStatusResponse(
                otherUserId,
                request.SenderUserId == actorUserId ? "request_sent" : "request_received",
                request.Id);
        }

        var friendUserIds = await dbContext.Friendships.AsNoTracking()
            .Where(friendship =>
                (friendship.UserId1 == actorUserId && relationshipUserIds.Contains(friendship.UserId2)) ||
                (friendship.UserId2 == actorUserId && relationshipUserIds.Contains(friendship.UserId1)))
            .Select(friendship => friendship.UserId1 == actorUserId
                ? friendship.UserId2
                : friendship.UserId1)
            .ToListAsync(cancellationToken);
        foreach (var friendUserId in friendUserIds)
        {
            statuses[friendUserId] = new RelationshipStatusResponse(friendUserId, "friends");
        }

        var blockedUserIds = await dbContext.BlockedUsers.AsNoTracking()
            .Where(block =>
                (block.BlockerUserId == actorUserId && relationshipUserIds.Contains(block.BlockedUserId)) ||
                (block.BlockedUserId == actorUserId && relationshipUserIds.Contains(block.BlockerUserId)))
            .Select(block => block.BlockerUserId == actorUserId
                ? block.BlockedUserId
                : block.BlockerUserId)
            .ToListAsync(cancellationToken);
        foreach (var blockedUserId in blockedUserIds)
        {
            statuses[blockedUserId] = new RelationshipStatusResponse(blockedUserId, "blocked");
        }

        return statuses;
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
        var followedUserIds = await dbContext.UserFollows.AsNoTracking()
            .Where(follow => follow.FollowerUserId == userId &&
                !dbContext.BlockedUsers.Any(block =>
                    (block.BlockerUserId == userId && block.BlockedUserId == follow.FollowingUserId) ||
                    (block.BlockerUserId == follow.FollowingUserId && block.BlockedUserId == userId)))
            .Select(follow => follow.FollowingUserId)
            .ToHashSetAsync(cancellationToken);

        return new RelationshipAccessSnapshot(friendUserIds, blockedUserIds, followedUserIds);
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

        if (!await CanSendFriendRequestAsync(senderUserId, receiverUserId, cancellationToken))
        {
            return Map(await RollbackFailureAsync<FriendRequestResponse>(
                transaction,
                FriendsOperationError.FriendRequestRestricted,
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
        var generalNotification = await notificationService.QueueAsync(
            receiverUserId,
            senderUserId,
            NotificationType.FriendRequestReceived,
            NotificationEntityType.FriendRequest,
            request.Id,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await PublishNotificationAsync(notification, cancellationToken);
        if (generalNotification is not null)
        {
            await notificationService.PublishAsync(generalNotification, cancellationToken);
        }
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
        var follows = await dbContext.UserFollows
            .Where(follow =>
                (follow.FollowerUserId == request.SenderUserId && follow.FollowingUserId == request.ReceiverUserId) ||
                (follow.FollowerUserId == request.ReceiverUserId && follow.FollowingUserId == request.SenderUserId))
            .ToListAsync(cancellationToken);
        if (!follows.Any(follow =>
                follow.FollowerUserId == request.SenderUserId &&
                follow.FollowingUserId == request.ReceiverUserId))
        {
            dbContext.UserFollows.Add(UserFollow.Create(
                request.SenderUserId,
                request.ReceiverUserId,
                now));
        }

        if (!follows.Any(follow =>
                follow.FollowerUserId == request.ReceiverUserId &&
                follow.FollowingUserId == request.SenderUserId))
        {
            dbContext.UserFollows.Add(UserFollow.Create(
                request.ReceiverUserId,
                request.SenderUserId,
                now));
        }

        var generalNotification = await notificationService.QueueAsync(
            request.SenderUserId,
            actorUserId,
            NotificationType.FriendRequestAccepted,
            NotificationEntityType.FriendRequest,
            request.Id,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await PublishNotificationAsync(notification, cancellationToken);
        if (generalNotification is not null)
        {
            await notificationService.PublishAsync(generalNotification, cancellationToken);
        }
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

    private async Task<FriendsOperationError> FollowCoreAsync(
        Guid actorUserId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        var pair = UserPair.Create(actorUserId, targetUserId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquirePairLockAsync(pair, cancellationToken);

        var target = await (from user in dbContext.Users.AsNoTracking()
                            join profile in dbContext.UserProfiles.AsNoTracking()
                                on user.Id equals profile.UserId into profiles
                            from profile in profiles.DefaultIfEmpty()
                            where user.Id == targetUserId
                            select new { user.IsActive, HasProfile = profile != null })
            .SingleOrDefaultAsync(cancellationToken);
        if (target is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return FriendsOperationError.UserNotFound;
        }

        if (!target.IsActive || !target.HasProfile)
        {
            await transaction.RollbackAsync(cancellationToken);
            return FriendsOperationError.UserIneligible;
        }

        if (await IsBlockedAsync(actorUserId, targetUserId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return FriendsOperationError.RelationshipBlocked;
        }

        if (await dbContext.UserFollows.AnyAsync(follow =>
                follow.FollowerUserId == actorUserId && follow.FollowingUserId == targetUserId,
                cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return FriendsOperationError.None;
        }

        dbContext.UserFollows.Add(UserFollow.Create(actorUserId, targetUserId, timeProvider.GetUtcNow()));
        Notification? generalNotification = null;
        if (!await FriendshipExistsAsync(pair, cancellationToken))
        {
            generalNotification = await notificationService.QueueAsync(
                targetUserId,
                actorUserId,
                NotificationType.UserFollowed,
                NotificationEntityType.UserFollow,
                actorUserId,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (generalNotification is not null)
        {
            await notificationService.PublishAsync(generalNotification, cancellationToken);
        }

        return FriendsOperationError.None;
    }

    private async Task<FriendsOperationError> UnfollowCoreAsync(
        Guid actorUserId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        var pair = UserPair.Create(actorUserId, targetUserId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquirePairLockAsync(pair, cancellationToken);
        var follow = await dbContext.UserFollows.SingleOrDefaultAsync(item =>
            item.FollowerUserId == actorUserId && item.FollowingUserId == targetUserId,
            cancellationToken);
        if (follow is not null)
        {
            dbContext.UserFollows.Remove(follow);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

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

        var follows = await dbContext.UserFollows.Where(follow =>
                (follow.FollowerUserId == actorUserId && follow.FollowingUserId == blockedUserId) ||
                (follow.FollowerUserId == blockedUserId && follow.FollowingUserId == actorUserId))
            .ToListAsync(cancellationToken);
        dbContext.UserFollows.RemoveRange(follows);

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

    private async Task<ApplicationResult<CursorPageResponse<UserFollowResponse>>> GetFollowPageAsync(
        Guid viewerUserId,
        Guid targetUserId,
        string direction,
        string? cursorValue,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > MaximumFollowPageSize)
        {
            return ApplicationResult<CursorPageResponse<UserFollowResponse>>.Failure(
                FollowValidation("invalid_follow_limit", $"Limit must be between 1 and {MaximumFollowPageSize}."));
        }

        if (!await CanViewRelationshipListAsync(
                viewerUserId, targetUserId, settings => settings.FollowListVisibility, cancellationToken))
        {
            return ApplicationResult<CursorPageResponse<UserFollowResponse>>.Failure(
                ToApplicationError(FriendsOperationError.UserNotFound));
        }

        FollowCursor? cursor;
        try
        {
            cursor = DecodeFollowCursor(cursorValue, viewerUserId, direction, targetUserId);
        }
        catch (FormatException)
        {
            return ApplicationResult<CursorPageResponse<UserFollowResponse>>.Failure(
                FollowValidation("invalid_follow_cursor", "The follow cursor is invalid."));
        }

        var isFollowers = direction == "followers";
        var query =
            from follow in dbContext.UserFollows.AsNoTracking()
            join profile in dbContext.UserProfiles.AsNoTracking()
                on (isFollowers ? follow.FollowerUserId : follow.FollowingUserId) equals profile.UserId
            join user in dbContext.Users.AsNoTracking() on profile.UserId equals user.Id
            where (isFollowers ? follow.FollowingUserId : follow.FollowerUserId) == targetUserId &&
                  user.IsActive &&
                  !dbContext.BlockedUsers.AsNoTracking().Any(block =>
                      (block.BlockerUserId == viewerUserId && block.BlockedUserId == profile.UserId) ||
                      (block.BlockerUserId == profile.UserId && block.BlockedUserId == viewerUserId))
            select new
            {
                profile.UserId,
                profile.Username,
                profile.DisplayName,
                profile.AvatarUrl,
                profile.AvatarMediaId,
                follow.FollowedAtUtc
            };
        var total = await query.CountAsync(cancellationToken);
        if (cursor is not null)
        {
            query = query.Where(item =>
                item.FollowedAtUtc < cursor.FollowedAtUtc ||
                (item.FollowedAtUtc == cursor.FollowedAtUtc &&
                 item.UserId.CompareTo(cursor.UserId) < 0));
        }

        var rows = await query
            .OrderByDescending(item => item.FollowedAtUtc)
            .ThenByDescending(item => item.UserId)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        var items = rows.Select(item => new FollowListItem(
            item.UserId,
            item.Username,
            item.DisplayName,
            item.AvatarUrl,
            item.AvatarMediaId,
            item.FollowedAtUtc)).ToList();
        var page = items.Take(limit).ToList();
        var nextCursor = items.Count > limit
            ? EncodeFollowCursor(
                new FollowCursor(page[^1].FollowedAtUtc, page[^1].UserId),
                viewerUserId,
                direction,
                targetUserId)
            : null;
        return ApplicationResult<CursorPageResponse<UserFollowResponse>>.Success(
            new CursorPageResponse<UserFollowResponse>(
                page.Select(item => new UserFollowResponse(
                    item.UserId,
                    item.Username,
                    item.DisplayName,
                    item.AvatarMediaId is null
                        ? item.AvatarUrl
                        : $"/api/users/{item.UserId}/avatar",
                    item.FollowedAtUtc)).ToList(),
                nextCursor,
                total));
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

    private async Task<bool> CanViewRelationshipListAsync(
        Guid viewerUserId,
        Guid ownerUserId,
        Func<UserPrivacySettings, RelationshipListVisibility> visibilitySelector,
        CancellationToken cancellationToken)
    {
        var ownerExists = await (from user in dbContext.Users.AsNoTracking()
                                 join profile in dbContext.UserProfiles.AsNoTracking() on user.Id equals profile.UserId
                                 where user.Id == ownerUserId && user.IsActive &&
                                       !dbContext.BlockedUsers.AsNoTracking().Any(block =>
                                           (block.BlockerUserId == viewerUserId && block.BlockedUserId == ownerUserId) ||
                                           (block.BlockerUserId == ownerUserId && block.BlockedUserId == viewerUserId))
                                 select user.Id).AnyAsync(cancellationToken);
        if (!ownerExists || viewerUserId == ownerUserId)
        {
            return ownerExists;
        }

        var settings = await dbContext.UserPrivacySettings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == ownerUserId, cancellationToken);
        return (settings is null ? RelationshipListVisibility.Public : visibilitySelector(settings)) switch
        {
            RelationshipListVisibility.Public => true,
            RelationshipListVisibility.Friends => await FriendshipExistsAsync(
                UserPair.Create(viewerUserId, ownerUserId), cancellationToken),
            _ => false
        };
    }

    private async Task<bool> CanSendFriendRequestAsync(
        Guid senderUserId,
        Guid receiverUserId,
        CancellationToken cancellationToken)
    {
        var policy = await dbContext.UserPrivacySettings.AsNoTracking()
            .Where(settings => settings.UserId == receiverUserId)
            .Select(settings => (FriendRequestPolicy?)settings.FriendRequestPolicy)
            .SingleOrDefaultAsync(cancellationToken) ?? FriendRequestPolicy.Everyone;
        return policy != FriendRequestPolicy.FriendsOfFriends ||
               await FriendIds(senderUserId).Intersect(FriendIds(receiverUserId)).AnyAsync(cancellationToken);
    }

    private FollowCursor? DecodeFollowCursor(
        string? value,
        Guid viewerUserId,
        string direction,
        Guid targetUserId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            if (value.Length is < 1 or > 4096)
            {
                throw new FormatException();
            }

            return JsonSerializer.Deserialize<FollowCursor>(
                CreateFollowCursorProtector(viewerUserId, direction, targetUserId).Unprotect(value))
                ?? throw new FormatException();
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or ArgumentException)
        {
            throw new FormatException("The follow cursor is invalid.", exception);
        }
    }

    private string EncodeFollowCursor(
        FollowCursor cursor,
        Guid viewerUserId,
        string direction,
        Guid targetUserId) =>
        CreateFollowCursorProtector(viewerUserId, direction, targetUserId)
            .Protect(JsonSerializer.Serialize(cursor));

    private IDataProtector CreateFollowCursorProtector(
        Guid viewerUserId,
        string direction,
        Guid targetUserId) =>
        protectionProvider.CreateProtector(
            "Fookbase.Follows",
            FollowCursorVersion.ToString(),
            viewerUserId.ToString("N"),
            direction,
            targetUserId.ToString("N"));

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
                ErrorCode.InvalidPagination,
                $"Offset must be non-negative and limit must be between 1 and {MaximumLimit}.",
                ApplicationErrorType.Validation);
        }

        return null;
    }

    private static ApplicationError FollowValidation(string code, string message) =>
        new(code, message, ApplicationErrorType.Validation);

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
        FriendsOperationError.UserNotFound => new(
            "user_not_found", "The user was not found.", ApplicationErrorType.NotFound),
        FriendsOperationError.UserIneligible => new(
            "user_ineligible", "The user is not eligible for this relationship operation.", ApplicationErrorType.Conflict),
        FriendsOperationError.NotificationNotFound => new(
            "notification_not_found", "The notification was not found.", ApplicationErrorType.NotFound),
        FriendsOperationError.FriendshipNotFound => new(
            "friendship_not_found", "The friendship was not found.", ApplicationErrorType.NotFound),
        FriendsOperationError.Forbidden => new(
            ErrorCode.Forbidden, "You are not allowed to perform this operation.", ApplicationErrorType.Forbidden),
        FriendsOperationError.AlreadyFriends => new(
            "already_friends", "The users are already friends.", ApplicationErrorType.Conflict),
        FriendsOperationError.PendingRequestExists => new(
            "pending_request_exists", "A pending friend request already exists.", ApplicationErrorType.Conflict),
        FriendsOperationError.RelationshipBlocked => new(
            "relationship_unavailable", "This relationship operation is unavailable.", ApplicationErrorType.Conflict),
        FriendsOperationError.FriendRequestRestricted => new(
            "friend_request_restricted", "This user only accepts requests from friends of friends.", ApplicationErrorType.Forbidden),
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
        UserNotFound,
        UserIneligible,
        NotificationNotFound,
        FriendshipNotFound,
        Forbidden,
        AlreadyFriends,
        PendingRequestExists,
        RelationshipBlocked,
        FriendRequestRestricted,
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

    private sealed record FollowCursor(DateTimeOffset FollowedAtUtc, Guid UserId);

    private sealed record FollowListItem(
        Guid UserId,
        string Username,
        string DisplayName,
        string? AvatarUrl,
        Guid? AvatarMediaId,
        DateTimeOffset FollowedAtUtc);
}

public sealed record RelationshipAccessSnapshot(
    IReadOnlySet<Guid> FriendUserIds,
    IReadOnlySet<Guid> BlockedUserIds,
    IReadOnlySet<Guid> FollowedUserIds);
