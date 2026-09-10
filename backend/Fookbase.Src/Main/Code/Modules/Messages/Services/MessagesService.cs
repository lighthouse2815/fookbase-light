using Fookbase.Api.Modules.Messages.Common;
using Fookbase.Api.Modules.Messages.Data;
using Fookbase.Api.Modules.Messages.DTOs.Responses;
using Fookbase.Api.Modules.Messages.Entities;
using Fookbase.Api.Modules.Messages.Hubs;
using Fookbase.Api.Modules.Friends.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Messages.Services;

public sealed class MessagesService(
    MessagesDbContext dbContext,
    TimeProvider timeProvider,
    IHubContext<MessagesHub> hubContext,
    FriendsService friendsService)
{
    private const int MaximumLimit = 100;
    private const int MaximumContentLength = 5000;

    public async Task<ApplicationResult<ConversationResponse>> GetOrCreateConversationAsync(
        Guid actorUserId,
        Guid participantUserId,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == participantUserId)
        {
            return ApplicationResult<ConversationResponse>.Failure(new ApplicationError(
                "cannot_message_self",
                "You cannot create a conversation with yourself.",
                ApplicationErrorType.Validation));
        }

        var relationshipError = await ValidateMessageRelationshipAsync(
            actorUserId,
            participantUserId,
            cancellationToken);
        if (relationshipError is not null)
        {
            return ApplicationResult<ConversationResponse>.Failure(relationshipError);
        }

        var conversation = await FindConversationAsync(actorUserId, participantUserId, cancellationToken);
        if (conversation is null)
        {
            var now = timeProvider.GetUtcNow();
            conversation = Conversation.Create(Guid.NewGuid(), actorUserId, participantUserId, now);
            dbContext.Conversations.Add(conversation);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                dbContext.Entry(conversation).State = EntityState.Detached;
                conversation = await FindConversationAsync(actorUserId, participantUserId, cancellationToken);
                if (conversation is null)
                {
                    return ApplicationResult<ConversationResponse>.Failure(new ApplicationError(
                        "conversation_creation_conflict",
                        "The conversation was created concurrently. Please try again.",
                        ApplicationErrorType.Conflict));
                }
            }
        }

        return ApplicationResult<ConversationResponse>.Success(
            ToConversationResponse(conversation!, actorUserId, null, 0));
    }

    public async Task<ApplicationResult<PagedResponse<ConversationResponse>>> GetConversationsAsync(
        Guid actorUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var paginationError = ValidatePagination(offset, limit);
        if (paginationError is not null)
        {
            return ApplicationResult<PagedResponse<ConversationResponse>>.Failure(paginationError);
        }

        var accessSnapshot = await friendsService.GetAccessSnapshotAsync(actorUserId, cancellationToken);
        var permittedUserIds = accessSnapshot.FriendUserIds
            .Except(accessSnapshot.BlockedUserIds)
            .ToArray();
        if (permittedUserIds.Length == 0)
        {
            return ApplicationResult<PagedResponse<ConversationResponse>>.Success(
                new PagedResponse<ConversationResponse>([], offset, limit, 0));
        }

        var query = dbContext.Conversations.AsNoTracking()
            .Where(conversation =>
                (conversation.UserId1 == actorUserId && permittedUserIds.Contains(conversation.UserId2))
                || (conversation.UserId2 == actorUserId && permittedUserIds.Contains(conversation.UserId1)));
        var total = await query.CountAsync(cancellationToken);
        var conversations = await query
            .OrderByDescending(conversation => conversation.LastMessageAtUtc)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
        var conversationIds = conversations.Select(conversation => conversation.Id).ToArray();
        var latestMessages = conversationIds.Length == 0
            ? new Dictionary<Guid, Message>()
            : (await dbContext.Messages.AsNoTracking()
                .Where(message => conversationIds.Contains(message.ConversationId))
                .GroupBy(message => message.ConversationId)
                .Select(group => group
                    .OrderByDescending(message => message.CreatedAtUtc)
                    .ThenByDescending(message => message.Id)
                    .First())
                .ToListAsync(cancellationToken))
                .ToDictionary(message => message.ConversationId);
        var unreadCounts = conversationIds.Length == 0
            ? new Dictionary<Guid, int>()
            : await dbContext.Messages.AsNoTracking()
                .Where(message =>
                    conversationIds.Contains(message.ConversationId)
                    && message.SenderUserId != actorUserId
                    && message.ReadAtUtc == null)
                .GroupBy(message => message.ConversationId)
                .Select(group => new { ConversationId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.ConversationId, item => item.Count, cancellationToken);
        var items = conversations
            .Select(conversation => ToConversationResponse(
                conversation,
                actorUserId,
                latestMessages.GetValueOrDefault(conversation.Id),
                unreadCounts.GetValueOrDefault(conversation.Id)))
            .ToArray();

        return ApplicationResult<PagedResponse<ConversationResponse>>.Success(
            new PagedResponse<ConversationResponse>(items, offset, limit, total));
    }

    public async Task<ApplicationResult<PagedResponse<IncomingMessageResponse>>> GetUnreadNotificationsAsync(
        Guid actorUserId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var paginationError = ValidatePagination(offset, limit);
        if (paginationError is not null)
        {
            return ApplicationResult<PagedResponse<IncomingMessageResponse>>.Failure(paginationError);
        }

        var accessSnapshot = await friendsService.GetAccessSnapshotAsync(actorUserId, cancellationToken);
        var permittedUserIds = accessSnapshot.FriendUserIds
            .Except(accessSnapshot.BlockedUserIds)
            .ToArray();
        if (permittedUserIds.Length == 0)
        {
            return ApplicationResult<PagedResponse<IncomingMessageResponse>>.Success(
                new PagedResponse<IncomingMessageResponse>([], offset, limit, 0));
        }

        var query = from notification in dbContext.MessageNotifications.AsNoTracking()
                    join conversation in dbContext.Conversations.AsNoTracking()
                        on notification.ConversationId equals conversation.Id
                    where notification.RecipientUserId == actorUserId
                          && notification.ReadAtUtc == null
                          && ((conversation.UserId1 == actorUserId
                               && permittedUserIds.Contains(conversation.UserId2))
                              || (conversation.UserId2 == actorUserId
                                  && permittedUserIds.Contains(conversation.UserId1)))
                    select notification;
        var total = await query.CountAsync(cancellationToken);
        var notifications = await query
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
        var conversationIds = notifications.Select(notification => notification.ConversationId).Distinct().ToArray();
        var messageIds = notifications.Select(notification => notification.MessageId).ToArray();
        var conversations = await dbContext.Conversations.AsNoTracking()
            .Where(conversation => conversationIds.Contains(conversation.Id))
            .ToDictionaryAsync(conversation => conversation.Id, cancellationToken);
        var messages = await dbContext.Messages.AsNoTracking()
            .Where(message => messageIds.Contains(message.Id))
            .ToDictionaryAsync(message => message.Id, cancellationToken);
        var unreadCounts = await dbContext.MessageNotifications.AsNoTracking()
            .Where(notification =>
                notification.RecipientUserId == actorUserId
                && notification.ReadAtUtc == null
                && conversationIds.Contains(notification.ConversationId))
            .GroupBy(notification => notification.ConversationId)
            .Select(group => new { ConversationId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ConversationId, item => item.Count, cancellationToken);
        var items = notifications
            .Where(notification =>
                conversations.ContainsKey(notification.ConversationId)
                && messages.ContainsKey(notification.MessageId))
            .Select(notification => new IncomingMessageResponse(
                ToConversationResponse(
                    conversations[notification.ConversationId],
                    actorUserId,
                    messages[notification.MessageId],
                    unreadCounts.GetValueOrDefault(notification.ConversationId)),
                ToMessageResponse(messages[notification.MessageId])))
            .ToArray();

        return ApplicationResult<PagedResponse<IncomingMessageResponse>>.Success(
            new PagedResponse<IncomingMessageResponse>(items, offset, limit, total));
    }

    public async Task<ApplicationResult<PagedResponse<MessageResponse>>> GetMessagesAsync(
        Guid actorUserId,
        Guid conversationId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var paginationError = ValidatePagination(offset, limit);
        if (paginationError is not null)
        {
            return ApplicationResult<PagedResponse<MessageResponse>>.Failure(paginationError);
        }

        var conversation = await dbContext.Conversations.SingleOrDefaultAsync(
            item => item.Id == conversationId,
            cancellationToken);
        var accessError = ValidateAccess(conversation, actorUserId);
        if (accessError is not null)
        {
            return ApplicationResult<PagedResponse<MessageResponse>>.Failure(accessError);
        }

        var relationshipError = await ValidateMessageRelationshipAsync(
            actorUserId,
            conversation!.OtherUserId(actorUserId),
            cancellationToken);
        if (relationshipError is not null)
        {
            return ApplicationResult<PagedResponse<MessageResponse>>.Failure(relationshipError);
        }

        var unreadMessages = await dbContext.Messages
            .Where(message =>
                message.ConversationId == conversationId
                && message.SenderUserId != actorUserId
                && message.ReadAtUtc == null)
            .ToListAsync(cancellationToken);
        if (unreadMessages.Count > 0)
        {
            var now = timeProvider.GetUtcNow();
            foreach (var message in unreadMessages)
            {
                message.MarkRead(now);
            }

            var unreadMessageIds = unreadMessages.Select(message => message.Id).ToArray();
            var notifications = await dbContext.MessageNotifications
                .Where(notification =>
                    notification.RecipientUserId == actorUserId
                    && unreadMessageIds.Contains(notification.MessageId)
                    && notification.ReadAtUtc == null)
                .ToListAsync(cancellationToken);
            foreach (var notification in notifications)
            {
                notification.MarkRead(now);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var query = dbContext.Messages.AsNoTracking()
            .Where(message => message.ConversationId == conversationId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(message => message.CreatedAtUtc)
            .ThenBy(message => message.Id)
            .Skip(offset)
            .Take(limit)
            .Select(message => new MessageResponse(
                message.Id,
                message.ConversationId,
                message.SenderUserId,
                message.Content,
                message.CreatedAtUtc,
                message.ReadAtUtc))
            .ToListAsync(cancellationToken);

        return ApplicationResult<PagedResponse<MessageResponse>>.Success(
            new PagedResponse<MessageResponse>(items, offset, limit, total));
    }

    public async Task<ApplicationResult<MessageResponse>> SendMessageAsync(
        Guid actorUserId,
        Guid conversationId,
        string? content,
        CancellationToken cancellationToken = default)
    {
        var normalizedContent = content?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedContent) || normalizedContent.Length > MaximumContentLength)
        {
            return ApplicationResult<MessageResponse>.Failure(new ApplicationError(
                "invalid_message_content",
                $"Message content must contain between 1 and {MaximumContentLength} characters.",
                ApplicationErrorType.Validation));
        }

        var conversation = await dbContext.Conversations.SingleOrDefaultAsync(
            item => item.Id == conversationId,
            cancellationToken);
        var accessError = ValidateAccess(conversation, actorUserId);
        if (accessError is not null)
        {
            return ApplicationResult<MessageResponse>.Failure(accessError);
        }

        var relationshipError = await ValidateMessageRelationshipAsync(
            actorUserId,
            conversation!.OtherUserId(actorUserId),
            cancellationToken);
        if (relationshipError is not null)
        {
            return ApplicationResult<MessageResponse>.Failure(relationshipError);
        }

        var now = timeProvider.GetUtcNow();
        var message = Message.Create(Guid.NewGuid(), conversationId, actorUserId, normalizedContent, now);
        conversation!.RecordMessage(now);
        dbContext.Messages.Add(message);
        dbContext.MessageNotifications.Add(MessageNotification.Create(
            Guid.NewGuid(),
            conversation.OtherUserId(actorUserId),
            conversationId,
            message.Id,
            now));
        await dbContext.SaveChangesAsync(cancellationToken);

        var recipientUserId = conversation.OtherUserId(actorUserId);
        var unreadCount = await dbContext.Messages.AsNoTracking()
            .CountAsync(item =>
                item.ConversationId == conversationId
                && item.SenderUserId == actorUserId
                && item.ReadAtUtc == null,
                cancellationToken);
        var messageResponse = ToMessageResponse(message);
        await hubContext.Clients.User(recipientUserId.ToString()).SendAsync(
            "MessageReceived",
            new IncomingMessageResponse(
                ToConversationResponse(conversation, recipientUserId, message, unreadCount),
                messageResponse),
            cancellationToken);

        return ApplicationResult<MessageResponse>.Success(messageResponse);
    }

    public async Task NotifyTypingAsync(
        Guid actorUserId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await dbContext.Conversations.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == conversationId,
            cancellationToken);
        if (ValidateAccess(conversation, actorUserId) is not null || conversation is null)
        {
            return;
        }

        var recipientUserId = conversation.OtherUserId(actorUserId);
        if (await ValidateMessageRelationshipAsync(actorUserId, recipientUserId, cancellationToken) is not null)
        {
            return;
        }

        await hubContext.Clients.User(recipientUserId.ToString()).SendAsync(
            "TypingStarted",
            new MessageTypingResponse(conversationId, actorUserId),
            cancellationToken);
    }

    private async Task<Conversation?> FindConversationAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken)
    {
        var (userId1, userId2) = firstUserId.CompareTo(secondUserId) < 0
            ? (firstUserId, secondUserId)
            : (secondUserId, firstUserId);
        return await dbContext.Conversations.SingleOrDefaultAsync(
            conversation => conversation.UserId1 == userId1 && conversation.UserId2 == userId2,
            cancellationToken);
    }

    private static ApplicationError? ValidatePagination(int offset, int limit)
    {
        if (offset < 0 || limit is < 1 or > MaximumLimit)
        {
            return new ApplicationError(
                "invalid_pagination",
                $"Offset must be non-negative and limit must be between 1 and {MaximumLimit}.",
                ApplicationErrorType.Validation);
        }

        return null;
    }

    private static ApplicationError? ValidateAccess(Conversation? conversation, Guid actorUserId)
    {
        if (conversation is null)
        {
            return new ApplicationError(
                "conversation_not_found",
                "Conversation was not found.",
                ApplicationErrorType.NotFound);
        }

        return conversation.Contains(actorUserId)
            ? null
            : new ApplicationError(
                "conversation_access_denied",
                "You do not have access to this conversation.",
                ApplicationErrorType.Forbidden);
    }

    private async Task<ApplicationError?> ValidateMessageRelationshipAsync(
        Guid actorUserId,
        Guid otherUserId,
        CancellationToken cancellationToken)
    {
        var accessSnapshot = await friendsService.GetAccessSnapshotAsync(actorUserId, cancellationToken);
        if (accessSnapshot.BlockedUserIds.Contains(otherUserId))
        {
            return new ApplicationError(
                "messaging_blocked",
                "Messaging is unavailable because one of you has blocked the other.",
                ApplicationErrorType.Forbidden);
        }

        return accessSnapshot.FriendUserIds.Contains(otherUserId)
            ? null
            : new ApplicationError(
                "messaging_requires_friendship",
                "You can only message friends.",
                ApplicationErrorType.Forbidden);
    }

    private static ConversationResponse ToConversationResponse(
        Conversation conversation,
        Guid actorUserId,
        Message? lastMessage,
        int unreadCount) =>
        new(
            conversation.Id,
            conversation.OtherUserId(actorUserId),
            conversation.CreatedAtUtc,
            conversation.LastMessageAtUtc,
            lastMessage is null ? null : ToMessageResponse(lastMessage),
            unreadCount);

    private static MessageResponse ToMessageResponse(Message message) =>
        new(
            message.Id,
            message.ConversationId,
            message.SenderUserId,
            message.Content,
            message.CreatedAtUtc,
            message.ReadAtUtc);
}
