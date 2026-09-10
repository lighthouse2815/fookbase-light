using Fookbase.Api.Modules.Messages.Common;
using Fookbase.Api.Modules.Messages.Data;
using Fookbase.Api.Modules.Messages.DTOs.Requests;
using Fookbase.Api.Modules.Messages.DTOs.Responses;
using Fookbase.Api.Modules.Messages.Entities;
using Fookbase.Api.Modules.Messages.Hubs;
using Fookbase.Api.Modules.Friends.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace Fookbase.Api.Modules.Messages.Services;

public sealed class MessagesService(
    MessagesDbContext dbContext,
    TimeProvider timeProvider,
    IHubContext<MessagesHub> hubContext,
    FriendsService friendsService)
{
    private const int MaximumLimit = 100;
    private const int MaximumContentLength = 5000;
    private const int MaximumCursorLength = 256;

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
            dbContext.ConversationReadCursors.AddRange(
                ConversationReadCursor.Create(conversation.Id, conversation.UserId1),
                ConversationReadCursor.Create(conversation.Id, conversation.UserId2));

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                dbContext.ChangeTracker.Clear();
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
        var unreadCounts = await GetUnreadCountsAsync(actorUserId, conversationIds, cancellationToken);
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

    public async Task<ApplicationResult<MessageHistoryResponse>> GetMessagesAsync(
        Guid actorUserId,
        Guid conversationId,
        string? before,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var paginationError = ValidateHistoryPagination(before, limit, out var cursor);
        if (paginationError is not null)
        {
            return ApplicationResult<MessageHistoryResponse>.Failure(paginationError);
        }

        var conversation = await dbContext.Conversations.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == conversationId,
            cancellationToken);
        var accessError = ValidateAccess(conversation, actorUserId);
        if (accessError is not null)
        {
            return ApplicationResult<MessageHistoryResponse>.Failure(accessError);
        }

        var relationshipError = await ValidateMessageRelationshipAsync(
            actorUserId,
            conversation!.OtherUserId(actorUserId),
            cancellationToken);
        if (relationshipError is not null)
        {
            return ApplicationResult<MessageHistoryResponse>.Failure(relationshipError);
        }

        var query = dbContext.Messages.AsNoTracking()
            .Where(message => message.ConversationId == conversationId);
        if (cursor is not null)
        {
            query = query.Where(message =>
                message.CreatedAtUtc < cursor.CreatedAtUtc ||
                (message.CreatedAtUtc == cursor.CreatedAtUtc &&
                 message.Id.CompareTo(cursor.MessageId) < 0));
        }

        var fetchedMessages = await query
            .OrderByDescending(message => message.CreatedAtUtc)
            .ThenByDescending(message => message.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        var hasMore = fetchedMessages.Count > limit;
        var pageMessages = fetchedMessages.Take(limit).ToArray();
        var nextCursor = hasMore && pageMessages.Length > 0
            ? EncodeCursor(pageMessages[^1])
            : null;
        var items = pageMessages
            .Reverse()
            .Select(ToMessageResponse)
            .ToArray();

        return ApplicationResult<MessageHistoryResponse>.Success(
            new MessageHistoryResponse(items, nextCursor, hasMore));
    }

    public async Task<ApplicationResult<bool>> MarkConversationReadAsync(
        Guid actorUserId,
        Guid conversationId,
        MarkConversationReadRequest request,
        CancellationToken cancellationToken = default)
    {
        var conversation = await dbContext.Conversations.SingleOrDefaultAsync(
            item => item.Id == conversationId,
            cancellationToken);
        var accessError = ValidateAccess(conversation, actorUserId);
        if (accessError is not null)
        {
            return ApplicationResult<bool>.Failure(accessError);
        }

        var relationshipError = await ValidateMessageRelationshipAsync(
            actorUserId,
            conversation!.OtherUserId(actorUserId),
            cancellationToken);
        if (relationshipError is not null)
        {
            return ApplicationResult<bool>.Failure(relationshipError);
        }

        var lastReadMessage = request.LastReadMessageId is null
            ? await dbContext.Messages
                .Where(message => message.ConversationId == conversationId)
                .OrderByDescending(message => message.CreatedAtUtc)
                .ThenByDescending(message => message.Id)
                .FirstOrDefaultAsync(cancellationToken)
            : await dbContext.Messages.SingleOrDefaultAsync(
                message => message.ConversationId == conversationId &&
                           message.Id == request.LastReadMessageId.Value,
                cancellationToken);
        if (lastReadMessage is null)
        {
            return ApplicationResult<bool>.Failure(new ApplicationError(
                "message_not_found",
                "The message was not found in this conversation.",
                ApplicationErrorType.NotFound));
        }

        var readCursor = await dbContext.ConversationReadCursors.SingleOrDefaultAsync(
            cursor => cursor.ConversationId == conversationId && cursor.UserId == actorUserId,
            cancellationToken);
        if (readCursor is null)
        {
            readCursor = ConversationReadCursor.Create(conversationId, actorUserId);
            dbContext.ConversationReadCursors.Add(readCursor);
        }

        var readAtUtc = timeProvider.GetUtcNow();
        if (!readCursor.AdvanceTo(lastReadMessage.Id, lastReadMessage.CreatedAtUtc, readAtUtc))
        {
            return ApplicationResult<bool>.Success(true);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var messagesToMarkRead = dbContext.Messages.Where(message =>
            message.ConversationId == conversationId &&
            message.SenderUserId != actorUserId &&
            message.ReadAtUtc == null &&
            (message.CreatedAtUtc < lastReadMessage.CreatedAtUtc ||
             (message.CreatedAtUtc == lastReadMessage.CreatedAtUtc &&
              message.Id.CompareTo(lastReadMessage.Id) <= 0)));
        var messageIdsToMarkRead = await messagesToMarkRead
            .Select(message => message.Id)
            .ToArrayAsync(cancellationToken);
        await messagesToMarkRead.ExecuteUpdateAsync(
            setters => setters.SetProperty(message => message.ReadAtUtc, readAtUtc),
            cancellationToken);
        await dbContext.MessageNotifications
            .Where(notification =>
                notification.RecipientUserId == actorUserId &&
                notification.ReadAtUtc == null &&
                messageIdsToMarkRead.Contains(notification.MessageId))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(notification => notification.ReadAtUtc, readAtUtc),
                cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await hubContext.Clients.User(conversation.OtherUserId(actorUserId).ToString()).SendAsync(
            "MessagesRead",
            new MessagesReadResponse(conversationId, actorUserId, lastReadMessage.Id, readAtUtc),
            cancellationToken);

        return ApplicationResult<bool>.Success(true);
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
        var unreadCount = (await GetUnreadCountsAsync(
            recipientUserId,
            [conversationId],
            cancellationToken)).GetValueOrDefault(conversationId);
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

    private async Task<Dictionary<Guid, int>> GetUnreadCountsAsync(
        Guid actorUserId,
        IReadOnlyCollection<Guid> conversationIds,
        CancellationToken cancellationToken)
    {
        if (conversationIds.Count == 0)
        {
            return [];
        }

        return await (
            from message in dbContext.Messages.AsNoTracking()
            join cursor in dbContext.ConversationReadCursors.AsNoTracking()
                    .Where(item => item.UserId == actorUserId)
                on message.ConversationId equals cursor.ConversationId into matchingCursors
            from cursor in matchingCursors.DefaultIfEmpty()
            where conversationIds.Contains(message.ConversationId)
                  && message.SenderUserId != actorUserId
                  && (cursor == null ||
                      cursor.LastReadMessageCreatedAtUtc == null ||
                      cursor.LastReadMessageId == null ||
                      message.CreatedAtUtc > cursor.LastReadMessageCreatedAtUtc ||
                      (message.CreatedAtUtc == cursor.LastReadMessageCreatedAtUtc &&
                       message.Id.CompareTo(cursor.LastReadMessageId.Value) > 0))
            group message by message.ConversationId
            into messagesByConversation
            select new
            {
                ConversationId = messagesByConversation.Key,
                Count = messagesByConversation.Count()
            })
            .ToDictionaryAsync(item => item.ConversationId, item => item.Count, cancellationToken);
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

    private static ApplicationError? ValidateHistoryPagination(
        string? before,
        int limit,
        out MessageHistoryCursor? cursor)
    {
        cursor = null;
        if (limit is < 1 or > MaximumLimit)
        {
            return new ApplicationError(
                "invalid_pagination",
                $"Limit must be between 1 and {MaximumLimit}.",
                ApplicationErrorType.Validation);
        }

        if (string.IsNullOrWhiteSpace(before))
        {
            return null;
        }

        if (before.Length > MaximumCursorLength || !TryDecodeCursor(before, out cursor))
        {
            return new ApplicationError(
                "invalid_message_cursor",
                "The message history cursor is invalid.",
                ApplicationErrorType.Validation);
        }

        return null;
    }

    private static string EncodeCursor(Message message)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            new MessageHistoryCursor(message.CreatedAtUtc, message.Id));
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool TryDecodeCursor(string value, out MessageHistoryCursor? cursor)
    {
        cursor = null;
        try
        {
            var base64 = value
                .Replace('-', '+')
                .Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            cursor = JsonSerializer.Deserialize<MessageHistoryCursor>(
                Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
            return cursor is not null && cursor.MessageId != Guid.Empty;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
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

    private sealed record MessageHistoryCursor(DateTimeOffset CreatedAtUtc, Guid MessageId);
}
