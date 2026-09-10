using Fookbase.Api.Modules.Messages.Common;
using Fookbase.Api.Modules.Messages.Data;
using Fookbase.Api.Modules.Messages.DTOs.Responses;
using Fookbase.Api.Modules.Messages.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Messages.Services;

public sealed class MessagesService(
    MessagesDbContext dbContext,
    TimeProvider timeProvider)
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

        var query = dbContext.Conversations.AsNoTracking()
            .Where(conversation => conversation.UserId1 == actorUserId || conversation.UserId2 == actorUserId);
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

        var now = timeProvider.GetUtcNow();
        var message = Message.Create(Guid.NewGuid(), conversationId, actorUserId, normalizedContent, now);
        conversation!.RecordMessage(now);
        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<MessageResponse>.Success(ToMessageResponse(message));
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
