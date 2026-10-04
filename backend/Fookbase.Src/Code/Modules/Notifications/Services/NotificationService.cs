using Fookbase.Api.Shared.Common;
using System.Globalization;
using System.Text;
using Fookbase.Api.Modules.Notifications.DTOs.Responses;
using Fookbase.Api.Modules.Notifications.Entities;
using Fookbase.Api.Modules.Notifications.Hubs;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Notifications.Services;

public sealed class NotificationService(
    FookbaseDbContext dbContext,
    IHubContext<NotificationsHub> hubContext,
    TimeProvider timeProvider)
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    public async Task<Notification?> QueueAsync(
        Guid recipientUserId,
        Guid? actorUserId,
        NotificationType type,
        NotificationEntityType? entityType,
        Guid? entityId,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == recipientUserId)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        if (actorUserId is not null && entityType is not null && entityId is not null)
        {
            var unread = await dbContext.Notifications.SingleOrDefaultAsync(
                notification =>
                    notification.RecipientUserId == recipientUserId &&
                    notification.ActorUserId == actorUserId &&
                    notification.Type == type &&
                    notification.EntityType == entityType &&
                    notification.EntityId == entityId &&
                    !notification.IsRead,
                cancellationToken);
            if (unread is not null)
            {
                unread.Refresh(now);
                return unread;
            }
        }

        var notification = Notification.Create(
            Guid.NewGuid(),
            recipientUserId,
            actorUserId,
            type,
            entityType,
            entityId,
            now);
        dbContext.Notifications.Add(notification);
        return notification;
    }

    public async Task<NotificationPageResponse> GetNotificationsAsync(
        Guid recipientUserId,
        string? before,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var cursor = string.IsNullOrWhiteSpace(before)
            ? (NotificationCursor?)null
            : DecodeCursor(before);
        var query = dbContext.Notifications.AsNoTracking()
            .Where(notification => notification.RecipientUserId == recipientUserId);
        if (cursor is not null)
        {
            query = query.Where(notification =>
                notification.CreatedAtUtc < cursor.CreatedAtUtc ||
                (notification.CreatedAtUtc == cursor.CreatedAtUtc &&
                 notification.Id.CompareTo(cursor.Id) < 0));
        }

        var candidates = await query
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .ThenByDescending(notification => notification.Id)
            .ToListAsync(cancellationToken);

        var visible = new List<Notification>(limit + 1);
        foreach (var notification in candidates)
        {
            if (!await CanSurfaceAsync(notification, recipientUserId, cancellationToken))
            {
                continue;
            }

            visible.Add(notification);
            if (visible.Count == limit + 1)
            {
                break;
            }
        }

        var page = visible.Take(limit).ToList();
        var hasMore = visible.Count > limit;
        return new NotificationPageResponse(
            await ToResponsesAsync(page, cancellationToken),
            hasMore ? EncodeCursor(page[^1]) : null);
    }

    public async Task<int> GetUnreadCountAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        var unread = await dbContext.Notifications.AsNoTracking()
            .Where(notification => notification.RecipientUserId == recipientUserId && !notification.IsRead)
            .ToListAsync(cancellationToken);
        var count = 0;
        foreach (var notification in unread)
        {
            if (await CanSurfaceAsync(notification, recipientUserId, cancellationToken))
            {
                count++;
            }
        }

        return count;
    }

    public async Task<bool> MarkReadAsync(
        Guid recipientUserId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await dbContext.Notifications.SingleOrDefaultAsync(
            item => item.Id == notificationId && item.RecipientUserId == recipientUserId,
            cancellationToken);
        if (notification is null)
        {
            return false;
        }

        notification.MarkRead(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task MarkAllReadAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var unread = await dbContext.Notifications
            .Where(notification => notification.RecipientUserId == recipientUserId && !notification.IsRead)
            .ToListAsync(cancellationToken);
        foreach (var notification in unread)
        {
            notification.MarkRead(now);
        }

        if (unread.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task PublishAsync(
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await CanSurfaceAsync(notification, notification.RecipientUserId, cancellationToken))
            {
                return;
            }

            var response = (await ToResponsesAsync([notification], cancellationToken))[0];
            await hubContext.Clients.User(notification.RecipientUserId.ToString())
                .SendAsync("NotificationReceived", response, cancellationToken);
        }
        catch (Exception)
        {
            // Realtime delivery is best effort. Persisted notifications remain the source of truth.
        }
    }

    public static bool IsValidCursor(string? before)
    {
        if (string.IsNullOrWhiteSpace(before))
        {
            return true;
        }

        try
        {
            _ = DecodeCursor(before);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async Task<bool> CanSurfaceAsync(
        Notification notification,
        Guid recipientUserId,
        CancellationToken cancellationToken)
    {
        if (notification.ActorUserId is not null &&
            !await IsPageContentNotificationAsync(notification, cancellationToken) &&
            await dbContext.BlockedUsers.AsNoTracking().AnyAsync(
                block =>
                    (block.BlockerUserId == recipientUserId &&
                     block.BlockedUserId == notification.ActorUserId.Value) ||
                    (block.BlockerUserId == notification.ActorUserId.Value &&
                     block.BlockedUserId == recipientUserId),
                cancellationToken))
        {
            return false;
        }

        return notification.EntityType switch
        {
            NotificationEntityType.FRIEND_REQUEST => notification.EntityId is not null &&
                await dbContext.FriendRequests.AsNoTracking().AnyAsync(
                    request => request.Id == notification.EntityId.Value,
                    cancellationToken),
            NotificationEntityType.POST => notification.EntityId is not null &&
                await CanAccessPostAsync(recipientUserId, notification.EntityId.Value, cancellationToken),
            NotificationEntityType.COMMENT => notification.EntityId is not null &&
                await CanAccessCommentAsync(recipientUserId, notification.EntityId.Value, cancellationToken),
            NotificationEntityType.GROUP => notification.EntityId is not null &&
                await CanAccessGroupAsync(recipientUserId, notification.EntityId.Value, cancellationToken),
            NotificationEntityType.GROUP_JOIN_REQUEST => notification.EntityId is not null &&
                await CanSurfaceGroupJoinRequestAsync(
                    recipientUserId,
                    notification.EntityId.Value,
                    cancellationToken),
            NotificationEntityType.GROUP_INVITE => notification.EntityId is not null &&
                await CanSurfaceGroupInviteAsync(
                    recipientUserId,
                    notification.EntityId.Value,
                    cancellationToken),
            NotificationEntityType.STORY => notification.EntityId is not null &&
                await CanAccessStoryAsync(recipientUserId, notification.EntityId.Value, cancellationToken),
            NotificationEntityType.PAGE => notification.EntityId is not null &&
                await CanAccessPageAsync(recipientUserId, notification.EntityId.Value, cancellationToken),
            NotificationEntityType.PAGE_ROLE_INVITATION => notification.EntityId is not null &&
                await CanSurfacePageRoleInvitationAsync(recipientUserId, notification.EntityId.Value, cancellationToken),
            NotificationEntityType.EVENT => notification.EntityId is not null &&
                await CanSurfaceEventAsync(notification, recipientUserId, cancellationToken),
            _ => true
        };
    }

    private Task<bool> IsPageContentNotificationAsync(Notification notification, CancellationToken cancellationToken)
    {
        if (notification.EntityId is null)
        {
            return Task.FromResult(false);
        }

        return notification.EntityType switch
        {
            NotificationEntityType.POST => dbContext.Posts.AsNoTracking().AnyAsync(post =>
                post.Id == notification.EntityId.Value && post.ContainerType == PostContainerType.PAGE, cancellationToken),
            NotificationEntityType.COMMENT =>
                (from comment in dbContext.Comments.AsNoTracking()
                 join post in dbContext.Posts.AsNoTracking() on comment.PostId equals post.Id
                 where comment.Id == notification.EntityId.Value && post.ContainerType == PostContainerType.PAGE
                 select comment.Id).AnyAsync(cancellationToken),
            _ => Task.FromResult(false)
        };
    }

    private async Task<bool> CanAccessStoryAsync(
        Guid recipientUserId,
        Guid storyId,
        CancellationToken cancellationToken)
    {
        var story = await dbContext.Stories.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == storyId && item.DeletedAtUtc == null,
            cancellationToken);
        if (story is null)
        {
            return false;
        }

        if (story.AuthorUserId == recipientUserId)
        {
            return true;
        }

        if (story.ExpiresAtUtc <= timeProvider.GetUtcNow() ||
            await dbContext.BlockedUsers.AsNoTracking().AnyAsync(
                block =>
                    (block.BlockerUserId == recipientUserId && block.BlockedUserId == story.AuthorUserId) ||
                    (block.BlockerUserId == story.AuthorUserId && block.BlockedUserId == recipientUserId),
                cancellationToken))
        {
            return false;
        }

        if (story.Privacy == PostPrivacy.PUBLIC)
        {
            return true;
        }

        return story.Privacy == PostPrivacy.FRIENDS &&
            await dbContext.Friendships.AsNoTracking().AnyAsync(
                friendship =>
                    (friendship.UserId1 == recipientUserId && friendship.UserId2 == story.AuthorUserId) ||
                    (friendship.UserId1 == story.AuthorUserId && friendship.UserId2 == recipientUserId),
                cancellationToken);
    }

    private async Task<bool> CanAccessCommentAsync(
        Guid recipientUserId,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var postId = await dbContext.Comments.AsNoTracking()
            .Where(comment => comment.Id == commentId && comment.DeletedAtUtc == null)
            .Select(comment => (Guid?)comment.PostId)
            .SingleOrDefaultAsync(cancellationToken);
        return postId is not null &&
            await CanAccessPostAsync(recipientUserId, postId.Value, cancellationToken);
    }

    private async Task<bool> CanAccessPostAsync(
        Guid recipientUserId,
        Guid postId,
        CancellationToken cancellationToken)
    {
        var post = await dbContext.Posts.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == postId && item.DeletedAtUtc == null,
            cancellationToken);
        if (post is null)
        {
            return false;
        }

        if (post.ContainerType == PostContainerType.GROUP)
        {
            return await CanAccessGroupAsync(recipientUserId, post.ContainerId, cancellationToken);
        }

        if (post.ContainerType == PostContainerType.PAGE)
        {
            return await CanAccessPageAsync(recipientUserId, post.ContainerId, cancellationToken);
        }

        if (post.AuthorUserId == recipientUserId)
        {
            return true;
        }

        if (await dbContext.BlockedUsers.AsNoTracking().AnyAsync(
                block =>
                    (block.BlockerUserId == recipientUserId && block.BlockedUserId == post.AuthorUserId) ||
                    (block.BlockerUserId == post.AuthorUserId && block.BlockedUserId == recipientUserId),
                cancellationToken))
        {
            return false;
        }

        if (post.Privacy == PostPrivacy.PUBLIC)
        {
            return true;
        }

        return post.Privacy == PostPrivacy.FRIENDS &&
            await dbContext.Friendships.AsNoTracking().AnyAsync(
                friendship =>
                    (friendship.UserId1 == recipientUserId && friendship.UserId2 == post.AuthorUserId) ||
                    (friendship.UserId1 == post.AuthorUserId && friendship.UserId2 == recipientUserId),
                cancellationToken);
    }

    private async Task<bool> CanAccessGroupAsync(
        Guid recipientUserId,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var group = await dbContext.Groups.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == groupId && item.DeletedAtUtc == null,
            cancellationToken);
        return group is not null &&
            (group.Privacy == Fookbase.Api.Modules.Groups.Entities.GroupPrivacy.PUBLIC ||
            await dbContext.GroupMembers.AsNoTracking().AnyAsync(
                member => member.GroupId == groupId && member.UserId == recipientUserId,
                cancellationToken));
    }

    private async Task<bool> CanAccessPageAsync(
        Guid recipientUserId,
        Guid pageId,
        CancellationToken cancellationToken)
    {
        var page = await dbContext.Pages.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == pageId && item.DeletedAtUtc == null,
            cancellationToken);
        return page is not null &&
            (page.Status == Fookbase.Api.Modules.Pages.Domain.Enums.PageStatus.PUBLISHED ||
             await dbContext.PageMembers.AsNoTracking().AnyAsync(
                 member => member.PageId == pageId && member.UserId == recipientUserId,
                 cancellationToken));
    }

    private Task<bool> CanSurfacePageRoleInvitationAsync(
        Guid recipientUserId,
        Guid invitationId,
        CancellationToken cancellationToken) =>
        (from invitation in dbContext.PageRoleInvitations.AsNoTracking()
         join page in dbContext.Pages.AsNoTracking() on invitation.PageId equals page.Id
         where invitation.Id == invitationId && invitation.InviteeUserId == recipientUserId &&
               invitation.Status == Fookbase.Api.Modules.Pages.Domain.Enums.PageRoleInvitationStatus.PENDING &&
               page.DeletedAtUtc == null
         select invitation.Id).AnyAsync(cancellationToken);

    private Task<bool> CanSurfaceGroupJoinRequestAsync(
        Guid recipientUserId,
        Guid requestId,
        CancellationToken cancellationToken) =>
        (from request in dbContext.GroupJoinRequests.AsNoTracking()
         join itemGroup in dbContext.Groups.AsNoTracking() on request.GroupId equals itemGroup.Id
         join member in dbContext.GroupMembers.AsNoTracking()
             on new { request.GroupId, UserId = recipientUserId }
             equals new { member.GroupId, member.UserId }
         where request.Id == requestId &&
               request.RequesterUserId == recipientUserId &&
               itemGroup.DeletedAtUtc == null
         select request.Id).AnyAsync(cancellationToken);

    private Task<bool> CanSurfaceGroupInviteAsync(
        Guid recipientUserId,
        Guid inviteId,
        CancellationToken cancellationToken) =>
        (from invite in dbContext.GroupInvites.AsNoTracking()
         join itemGroup in dbContext.Groups.AsNoTracking() on invite.GroupId equals itemGroup.Id
         where invite.Id == inviteId &&
               invite.InviteeUserId == recipientUserId &&
               itemGroup.DeletedAtUtc == null
         select invite.Id).AnyAsync(cancellationToken);

    private async Task<bool> CanSurfaceEventAsync(
        Notification notification,
        Guid recipientUserId,
        CancellationToken cancellationToken)
    {
        if (notification.Type == NotificationType.EVENT_INVITE)
        {
            return await dbContext.EventInvitations.AsNoTracking().AnyAsync(invitation =>
                invitation.Id == notification.EntityId && invitation.InviteeUserId == recipientUserId &&
                invitation.Status == Fookbase.Api.Modules.Events.Entities.EventInvitationStatus.PENDING &&
                dbContext.Events.Any(item => item.Id == invitation.EventId && item.DeletedAtUtc == null &&
                    item.Status == Fookbase.Api.Modules.Events.Entities.EventStatus.PUBLISHED), cancellationToken);
        }

        return await dbContext.Events.AsNoTracking().AnyAsync(item => item.Id == notification.EntityId &&
            item.DeletedAtUtc == null && item.Status != Fookbase.Api.Modules.Events.Entities.EventStatus.DRAFT &&
            (item.Privacy == Fookbase.Api.Modules.Events.Entities.EventPrivacy.PUBLIC ||
             dbContext.EventParticipants.Any(participant => participant.EventId == item.Id && participant.UserId == recipientUserId) ||
             dbContext.EventInvitations.Any(invitation => invitation.EventId == item.Id && invitation.InviteeUserId == recipientUserId &&
                 invitation.Status == Fookbase.Api.Modules.Events.Entities.EventInvitationStatus.PENDING)), cancellationToken);
    }

    private async Task<IReadOnlyList<NotificationResponse>> ToResponsesAsync(
        IReadOnlyList<Notification> notifications,
        CancellationToken cancellationToken)
    {
        var commentIds = notifications
            .Where(notification => notification.EntityType == NotificationEntityType.COMMENT && notification.EntityId is not null)
            .Select(notification => notification.EntityId!.Value)
            .Distinct()
            .ToArray();
        var commentPostIds = commentIds.Length == 0
            ? new Dictionary<Guid, Guid>()
            : await dbContext.Comments.AsNoTracking()
                .Where(comment => commentIds.Contains(comment.Id) && comment.DeletedAtUtc == null)
                .Select(comment => new { comment.Id, comment.PostId })
                .ToDictionaryAsync(item => item.Id, item => item.PostId, cancellationToken);
        var eventInviteIds = notifications
            .Where(notification => notification.Type == NotificationType.EVENT_INVITE && notification.EntityId is not null)
            .Select(notification => notification.EntityId!.Value)
            .Distinct()
            .ToArray();
        var eventInviteEventIds = eventInviteIds.Length == 0
            ? new Dictionary<Guid, Guid>()
            : await dbContext.EventInvitations.AsNoTracking()
                .Where(invitation => eventInviteIds.Contains(invitation.Id))
                .Select(invitation => new { invitation.Id, invitation.EventId })
                .ToDictionaryAsync(item => item.Id, item => item.EventId, cancellationToken);
        var groupInviteIds = notifications
            .Where(notification => notification.EntityType == NotificationEntityType.GROUP_INVITE && notification.EntityId is not null)
            .Select(notification => notification.EntityId!.Value)
            .Distinct()
            .ToArray();
        var groupInviteGroupIds = groupInviteIds.Length == 0
            ? new Dictionary<Guid, Guid>()
            : await dbContext.GroupInvites.AsNoTracking()
                .Where(invite => groupInviteIds.Contains(invite.Id))
                .ToDictionaryAsync(invite => invite.Id, invite => invite.GroupId, cancellationToken);
        var groupJoinRequestIds = notifications
            .Where(notification => notification.EntityType == NotificationEntityType.GROUP_JOIN_REQUEST && notification.EntityId is not null)
            .Select(notification => notification.EntityId!.Value)
            .Distinct()
            .ToArray();
        var groupJoinRequestGroupIds = groupJoinRequestIds.Length == 0
            ? new Dictionary<Guid, Guid>()
            : await dbContext.GroupJoinRequests.AsNoTracking()
                .Where(request => groupJoinRequestIds.Contains(request.Id))
                .ToDictionaryAsync(request => request.Id, request => request.GroupId, cancellationToken);
        var pageInvitationIds = notifications
            .Where(notification => notification.EntityType == NotificationEntityType.PAGE_ROLE_INVITATION && notification.EntityId is not null)
            .Select(notification => notification.EntityId!.Value)
            .Distinct()
            .ToArray();
        var pageInvitationTargets = pageInvitationIds.Length == 0
            ? new Dictionary<Guid, PageInvitationTarget>()
            : await (from invitation in dbContext.PageRoleInvitations.AsNoTracking()
                     join page in dbContext.Pages.AsNoTracking() on invitation.PageId equals page.Id
                     where pageInvitationIds.Contains(invitation.Id) && page.DeletedAtUtc == null
                     select new PageInvitationTarget(invitation.Id, page.Id, page.Username))
                .ToDictionaryAsync(target => target.InvitationId, cancellationToken);
        var actorUserIds = notifications
            .Where(notification => notification.ActorUserId is not null)
            .Select(notification => notification.ActorUserId!.Value)
            .Distinct()
            .ToArray();
        var profiles = actorUserIds.Length == 0
            ? new Dictionary<Guid, ActorProfile>()
            : await dbContext.UserProfiles.AsNoTracking()
                .Where(profile => actorUserIds.Contains(profile.UserId))
                .Select(profile => new ActorProfile(
                    profile.UserId,
                    profile.Username,
                    profile.DisplayName,
                    profile.AvatarMediaId == null ? profile.AvatarUrl : $"/api/users/{profile.UserId}/avatar"))
                .ToDictionaryAsync(profile => profile.UserId, cancellationToken);

        return notifications.Select(notification =>
        {
            var profile = notification.ActorUserId is null
                ? null
                : profiles.GetValueOrDefault(notification.ActorUserId.Value);
            var pageTarget = notification.EntityType == NotificationEntityType.PAGE_ROLE_INVITATION && notification.EntityId is Guid invitationId
                ? pageInvitationTargets.GetValueOrDefault(invitationId)
                : null;
            Guid? parentEntityId = notification.EntityId is Guid entityId
                ? notification.EntityType switch
                {
                    NotificationEntityType.COMMENT => commentPostIds.GetValueOrDefault(entityId),
                    NotificationEntityType.EVENT when notification.Type == NotificationType.EVENT_INVITE => eventInviteEventIds.GetValueOrDefault(entityId),
                    NotificationEntityType.GROUP_INVITE => groupInviteGroupIds.GetValueOrDefault(entityId),
                    NotificationEntityType.GROUP_JOIN_REQUEST => groupJoinRequestGroupIds.GetValueOrDefault(entityId),
                    NotificationEntityType.PAGE_ROLE_INVITATION => pageTarget?.PageId,
                    _ => null
                }
                : null;
            if (parentEntityId == Guid.Empty) parentEntityId = null;
            return new NotificationResponse(
                notification.Id,
                notification.RecipientUserId,
                notification.ActorUserId,
                profile?.Username,
                profile?.DisplayName,
                notification.Type.ToApiName(),
                notification.EntityType?.ToApiName(),
                notification.EntityId,
                parentEntityId,
                notification.IsRead,
                notification.CreatedAtUtc,
                notification.ReadAtUtc,
                profile?.AvatarUrl,
                pageTarget?.Username);
        }).ToList();
    }

    private static string EncodeCursor(Notification notification)
    {
        var payload = notification.CreatedAtUtc.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture) +
            ":" + notification.Id.ToString("N");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static NotificationCursor DecodeCursor(string value)
    {
        try
        {
            var encoded = value.Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight(encoded.Length + (4 - encoded.Length % 4) % 4, '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(encoded)).Split(':', 2);
            if (parts.Length != 2 ||
                !long.TryParse(parts[0], CultureInfo.InvariantCulture, out var ticks) ||
                !Guid.TryParseExact(parts[1], "N", out var id))
            {
                throw new FormatException("The notification cursor is invalid.");
            }

            return new NotificationCursor(
                new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc)),
                id);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("The notification cursor is invalid.", exception);
        }
    }

    private sealed record NotificationCursor(DateTimeOffset CreatedAtUtc, Guid Id);

    private sealed record ActorProfile(Guid UserId, string Username, string DisplayName, string? AvatarUrl);

    private sealed record PageInvitationTarget(Guid InvitationId, Guid PageId, string Username);
}
