using System.Text;
using System.Text.Json;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Media.DTOs.Responses;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Messages.Common;
using Fookbase.Api.Modules.Messages.DTOs.Requests;
using Fookbase.Api.Modules.Messages.DTOs.Responses;
using Fookbase.Api.Modules.Messages.Entities;
using Fookbase.Api.Modules.Messages.Hubs;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Stories.Entities;
using Fookbase.Api.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Messages.Services;

public sealed class MessagesService(
    FookbaseDbContext dbContext,
    TimeProvider timeProvider,
    IHubContext<MessagesHub> hubContext,
    FriendsService friendsService,
    MediaService mediaService)
{
    private const int MaximumLimit = 100;
    private const int MaximumContentLength = 5_000;
    private const int MaximumCursorLength = 256;
    private const int MaximumAttachmentCount = 10;
    private const int MaximumGroupSize = 50;

    public Task<ApplicationResult<ConversationResponse>> GetOrCreateConversationAsync(Guid actorUserId, Guid participantUserId, CancellationToken cancellationToken = default) =>
        GetOrCreateDirectConversationAsync(actorUserId, participantUserId, cancellationToken);

    public async Task<ApplicationResult<ConversationResponse>> GetOrCreateDirectConversationAsync(Guid actorUserId, Guid participantUserId, CancellationToken cancellationToken = default)
    {
        if (actorUserId == participantUserId)
            return Failure<ConversationResponse>("cannot_message_self", "You cannot create a conversation with yourself.");

        var relationshipError = await ValidateDirectRelationshipAsync(actorUserId, participantUserId, cancellationToken);
        if (relationshipError is not null) return ApplicationResult<ConversationResponse>.Failure(relationshipError);

        var conversation = await FindDirectConversationAsync(actorUserId, participantUserId, cancellationToken);
        if (conversation is null)
        {
            var now = timeProvider.GetUtcNow();
            conversation = Conversation.Create(Guid.NewGuid(), actorUserId, participantUserId, now);
            dbContext.Conversations.Add(conversation);
            dbContext.ConversationParticipants.AddRange(
                ConversationParticipant.Create(conversation.Id, conversation.UserId1!.Value, ConversationParticipantRole.Member, now),
                ConversationParticipant.Create(conversation.Id, conversation.UserId2!.Value, ConversationParticipantRole.Member, now));
            // Kept as a compatibility projection for existing direct chat clients/imports.
            dbContext.ConversationReadCursors.AddRange(
                ConversationReadCursor.Create(conversation.Id, conversation.UserId1.Value),
                ConversationReadCursor.Create(conversation.Id, conversation.UserId2.Value));
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                dbContext.ChangeTracker.Clear();
                conversation = await FindDirectConversationAsync(actorUserId, participantUserId, cancellationToken);
                if (conversation is null) return Conflict<ConversationResponse>("conversation_creation_conflict", "The conversation was created concurrently. Please try again.");
            }
        }

        var response = await BuildConversationResponseAsync(conversation!, actorUserId, cancellationToken);
        await hubContext.Clients.User(participantUserId.ToString()).SendAsync("ConversationCreated", response, cancellationToken);
        return ApplicationResult<ConversationResponse>.Success(response);
    }

    public async Task<ApplicationResult<ConversationResponse>> CreateGroupConversationAsync(Guid actorUserId, CreateGroupConversationRequest request, CancellationToken cancellationToken = default)
    {
        var participantIds = request.ParticipantUserIds?.ToArray() ?? [];
        if (participantIds.Length is < 1 or >= MaximumGroupSize || participantIds.Any(id => id == Guid.Empty || id == actorUserId) || participantIds.Distinct().Count() != participantIds.Length)
            return Failure<ConversationResponse>("invalid_group_participants", $"A group needs between 1 and {MaximumGroupSize - 1} distinct participants besides its creator.");
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 120)
            return Failure<ConversationResponse>("invalid_group_title", "A group title containing between 1 and 120 characters is required.");
        if (await dbContext.Users.AsNoTracking().CountAsync(user => participantIds.Contains(user.Id), cancellationToken) != participantIds.Length)
            return Failure<ConversationResponse>("group_participant_not_found", "Every group participant must be an existing user.", ApplicationErrorType.NotFound);
        if (request.PhotoMediaId is not null)
        {
            var media = await mediaService.ValidateGroupCoverImageAsync(actorUserId, request.PhotoMediaId.Value, cancellationToken);
            if (!media.Succeeded) return ApplicationResult<ConversationResponse>.Failure(ToMessagesError(media.Error!));
        }

        var now = timeProvider.GetUtcNow();
        Conversation conversation;
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            conversation = Conversation.CreateGroup(Guid.NewGuid(), request.Title, now);
            conversation.UpdateGroup(request.Title, request.PhotoMediaId);
            dbContext.Conversations.Add(conversation);
            dbContext.ConversationParticipants.Add(ConversationParticipant.Create(conversation.Id, actorUserId, ConversationParticipantRole.Owner, now));
            dbContext.ConversationParticipants.AddRange(participantIds.Select(id => ConversationParticipant.Create(conversation.Id, id, ConversationParticipantRole.Member, now)));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (ArgumentException error)
        {
            return Failure<ConversationResponse>("invalid_group_title", error.Message);
        }

        var response = await BuildConversationResponseAsync(conversation, actorUserId, cancellationToken);
        await hubContext.Clients.Users(participantIds.Append(actorUserId).Select(id => id.ToString())).SendAsync("ConversationCreated", response, cancellationToken);
        return ApplicationResult<ConversationResponse>.Success(response);
    }

    public async Task<ApplicationResult<PagedResponse<ConversationResponse>>> GetConversationsAsync(Guid actorUserId, string? before, int limit, bool includeArchived, CancellationToken cancellationToken = default)
    {
        var paginationError = ValidateConversationPagination(before, limit, out var cursor);
        if (paginationError is not null) return ApplicationResult<PagedResponse<ConversationResponse>>.Failure(paginationError);

        var snapshot = await friendsService.GetAccessSnapshotAsync(actorUserId, cancellationToken);
        var permittedDirectUsers = snapshot.FriendUserIds.Except(snapshot.BlockedUserIds).ToArray();
        var query = from participant in dbContext.ConversationParticipants.AsNoTracking()
                    join conversation in dbContext.Conversations.AsNoTracking() on participant.ConversationId equals conversation.Id
                    where participant.UserId == actorUserId && participant.LeftAtUtc == null &&
                          (includeArchived || participant.ArchivedAtUtc == null) &&
                          (conversation.Type != ConversationType.Direct ||
                           dbContext.ConversationParticipants.Any(peer => peer.ConversationId == conversation.Id && peer.UserId != actorUserId && peer.LeftAtUtc == null && permittedDirectUsers.Contains(peer.UserId)))
                    select new { Conversation = conversation, Participant = participant };
        if (cursor is not null)
            query = query.Where(item => item.Conversation.LastMessageAtUtc < cursor.LastMessageAtUtc ||
                (item.Conversation.LastMessageAtUtc == cursor.LastMessageAtUtc && item.Conversation.Id.CompareTo(cursor.ConversationId) < 0));
        var candidates = await query.OrderByDescending(item => item.Conversation.LastMessageAtUtc).ThenByDescending(item => item.Conversation.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var page = candidates.Take(limit).ToArray();
        var ids = page.Select(item => item.Conversation.Id).ToArray();
        var participants = await LoadParticipantMapAsync(ids, cancellationToken);
        var latestMessages = await LoadLatestMessagesAsync(ids, snapshot.BlockedUserIds, cancellationToken);
        var unreadCounts = await GetUnreadCountsAsync(actorUserId, ids, snapshot.BlockedUserIds, cancellationToken);
        var items = page.Select(item => ToConversationResponse(item.Conversation, actorUserId, item.Participant,
            latestMessages.GetValueOrDefault(item.Conversation.Id), unreadCounts.GetValueOrDefault(item.Conversation.Id),
            participants.GetValueOrDefault(item.Conversation.Id, []))).ToArray();
        var nextCursor = candidates.Count > limit && page.Length > 0 ? EncodeConversationCursor(page[^1].Conversation) : null;
        return ApplicationResult<PagedResponse<ConversationResponse>>.Success(new PagedResponse<ConversationResponse>(items, 0, limit, items.Length, nextCursor));
    }

    public async Task<ApplicationResult<ConversationResponse>> GetConversationAsync(Guid actorUserId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        var access = await GetActiveConversationAccessAsync(actorUserId, conversationId, cancellationToken);
        if (!access.Succeeded) return ApplicationResult<ConversationResponse>.Failure(access.Error!);
        var relationshipError = await ValidateConversationRelationshipAsync(access.Value!.Conversation, actorUserId, cancellationToken);
        return relationshipError is not null
            ? ApplicationResult<ConversationResponse>.Failure(relationshipError)
            : ApplicationResult<ConversationResponse>.Success(await BuildConversationResponseAsync(access.Value!.Conversation, actorUserId, cancellationToken));
    }

    public async Task<ApplicationResult<ConversationResponse>> UpdateConversationAsync(Guid actorUserId, Guid conversationId, UpdateConversationRequest request, CancellationToken cancellationToken = default)
    {
        var access = await GetActiveConversationAccessAsync(actorUserId, conversationId, cancellationToken);
        if (!access.Succeeded) return ApplicationResult<ConversationResponse>.Failure(access.Error!);
        var (conversation, participant) = access.Value!;
        var updateGroup = request.Title is not null || request.PhotoMediaId is not null || request.RemovePhoto;
        if (updateGroup)
        {
            if (conversation.Type != ConversationType.Group || !CanManageConversation(participant.Role)) return Forbidden<ConversationResponse>();
            if (request.PhotoMediaId is not null)
            {
                var media = await mediaService.ValidateGroupCoverImageAsync(actorUserId, request.PhotoMediaId.Value, cancellationToken);
                if (!media.Succeeded) return ApplicationResult<ConversationResponse>.Failure(ToMessagesError(media.Error!));
            }
            try { conversation.UpdateGroup(request.Title ?? conversation.Title!, request.RemovePhoto ? null : request.PhotoMediaId ?? conversation.PhotoMediaId); }
            catch (ArgumentException error) { return Failure<ConversationResponse>("invalid_group_title", error.Message); }
        }
        try
        {
            if (request.Archived is not null) participant.SetArchived(request.Archived.Value, timeProvider.GetUtcNow());
            if (request.MutedUntilUtc is not null) participant.SetMutedUntil(request.MutedUntilUtc);
            if (request.Nickname is not null) participant.SetNickname(request.Nickname);
        }
        catch (ArgumentException error) { return Failure<ConversationResponse>("invalid_participant_nickname", error.Message); }

        await dbContext.SaveChangesAsync(cancellationToken);
        var response = await BuildConversationResponseAsync(conversation, actorUserId, cancellationToken);
        await NotifyActiveParticipantsAsync(conversation, actorUserId, "ConversationUpdated", response, cancellationToken);
        return ApplicationResult<ConversationResponse>.Success(response);
    }

    public async Task<ApplicationResult<MessageHistoryResponse>> GetMessagesAsync(Guid actorUserId, Guid conversationId, string? before, int limit, CancellationToken cancellationToken = default)
    {
        var paginationError = ValidateMessagePagination(before, limit, out var cursor);
        if (paginationError is not null) return ApplicationResult<MessageHistoryResponse>.Failure(paginationError);
        var access = await GetActiveConversationAccessAsync(actorUserId, conversationId, cancellationToken);
        if (!access.Succeeded) return ApplicationResult<MessageHistoryResponse>.Failure(access.Error!);
        var relationshipError = await ValidateConversationRelationshipAsync(access.Value!.Conversation, actorUserId, cancellationToken);
        if (relationshipError is not null) return ApplicationResult<MessageHistoryResponse>.Failure(relationshipError);
        var blockedUserIds = (await friendsService.GetAccessSnapshotAsync(actorUserId, cancellationToken)).BlockedUserIds;
        var query = dbContext.Messages.AsNoTracking().Where(message => message.ConversationId == conversationId && !blockedUserIds.Contains(message.SenderUserId));
        if (cursor is not null)
            query = query.Where(message => message.CreatedAtUtc < cursor.CreatedAtUtc ||
                (message.CreatedAtUtc == cursor.CreatedAtUtc && message.Id.CompareTo(cursor.MessageId) < 0));
        var candidates = await query.OrderByDescending(message => message.CreatedAtUtc).ThenByDescending(message => message.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var page = candidates.Take(limit).ToArray();
        var items = (await BuildMessageResponsesAsync(page, actorUserId, blockedUserIds, cancellationToken)).Reverse().ToArray();
        return ApplicationResult<MessageHistoryResponse>.Success(new MessageHistoryResponse(items,
            candidates.Count > limit && page.Length > 0 ? EncodeMessageCursor(page[^1]) : null, candidates.Count > limit));
    }

    public async Task<ApplicationResult<bool>> MarkConversationReadAsync(Guid actorUserId, Guid conversationId, MarkConversationReadRequest request, CancellationToken cancellationToken = default)
    {
        var access = await GetActiveConversationAccessAsync(actorUserId, conversationId, cancellationToken);
        if (!access.Succeeded) return ApplicationResult<bool>.Failure(access.Error!);
        var (conversation, participant) = access.Value!;
        var relationshipError = await ValidateConversationRelationshipAsync(conversation, actorUserId, cancellationToken);
        if (relationshipError is not null) return ApplicationResult<bool>.Failure(relationshipError);
        var blockedUserIds = (await friendsService.GetAccessSnapshotAsync(actorUserId, cancellationToken)).BlockedUserIds;
        var lastRead = request.LastReadMessageId is null
            ? await dbContext.Messages.Where(message => message.ConversationId == conversationId && !blockedUserIds.Contains(message.SenderUserId)).OrderByDescending(message => message.CreatedAtUtc).ThenByDescending(message => message.Id).FirstOrDefaultAsync(cancellationToken)
            : await dbContext.Messages.SingleOrDefaultAsync(message => message.ConversationId == conversationId && message.Id == request.LastReadMessageId.Value && !blockedUserIds.Contains(message.SenderUserId), cancellationToken);
        if (lastRead is null) return Failure<bool>("message_not_found", "The message was not found in this conversation.", ApplicationErrorType.NotFound);
        var now = timeProvider.GetUtcNow();
        if (!participant.AdvanceReadCursor(lastRead.Id, lastRead.CreatedAtUtc, now)) return ApplicationResult<bool>.Success(true);
        if (conversation.Type == ConversationType.Direct)
        {
            var legacyCursor = await dbContext.ConversationReadCursors.SingleOrDefaultAsync(cursor => cursor.ConversationId == conversationId && cursor.UserId == actorUserId, cancellationToken);
            if (legacyCursor is null) { legacyCursor = ConversationReadCursor.Create(conversationId, actorUserId); dbContext.ConversationReadCursors.Add(legacyCursor); }
            legacyCursor.AdvanceTo(lastRead.Id, lastRead.CreatedAtUtc, now);
            await dbContext.Messages.Where(message => message.ConversationId == conversationId && message.SenderUserId != actorUserId && message.ReadAtUtc == null &&
                    (message.CreatedAtUtc < lastRead.CreatedAtUtc || (message.CreatedAtUtc == lastRead.CreatedAtUtc && message.Id.CompareTo(lastRead.Id) <= 0)))
                .ExecuteUpdateAsync(setters => setters.SetProperty(message => message.ReadAtUtc, now), cancellationToken);
        }
        await dbContext.MessageNotifications.Where(notification => notification.RecipientUserId == actorUserId && notification.ConversationId == conversationId && notification.ReadAtUtc == null && notification.CreatedAtUtc <= lastRead.CreatedAtUtc)
            .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.ReadAtUtc, now), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        var response = new MessagesReadResponse(conversationId, actorUserId, lastRead.Id, now);
        await NotifyActiveParticipantsAsync(conversation, actorUserId, "ReadCursorAdvanced", response, cancellationToken);
        await NotifyActiveParticipantsAsync(conversation, actorUserId, "MessagesRead", response, cancellationToken);
        return ApplicationResult<bool>.Success(true);
    }

    public Task<ApplicationResult<MessageResponse>> SendMessageAsync(
        Guid actorUserId,
        Guid conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default) =>
        SendMessageCoreAsync(actorUserId, conversationId, request, null, cancellationToken);

    public async Task<ApplicationResult<MessageResponse>> SendStoryReplyAsync(
        Guid actorUserId,
        Guid storyAuthorUserId,
        Guid storyId,
        string? content,
        CancellationToken cancellationToken = default)
    {
        var conversation = await GetOrCreateDirectConversationAsync(
            actorUserId, storyAuthorUserId, cancellationToken);
        if (!conversation.Succeeded)
        {
            return ApplicationResult<MessageResponse>.Failure(conversation.Error!);
        }

        return await SendMessageCoreAsync(
            actorUserId,
            conversation.Value!.Id,
            new SendMessageRequest(content),
            storyId,
            cancellationToken);
    }

    private async Task<ApplicationResult<MessageResponse>> SendMessageCoreAsync(
        Guid actorUserId,
        Guid conversationId,
        SendMessageRequest request,
        Guid? storyId,
        CancellationToken cancellationToken)
    {
        var attachmentIds = request.MediaIds?.ToArray() ?? [];
        var content = request.Content?.Trim();
        if ((string.IsNullOrWhiteSpace(content) && attachmentIds.Length == 0) || (!string.IsNullOrWhiteSpace(content) && content.Length > MaximumContentLength) || attachmentIds.Length > MaximumAttachmentCount || attachmentIds.Distinct().Count() != attachmentIds.Length)
            return Failure<MessageResponse>("invalid_message_content", $"A message needs text or up to {MaximumAttachmentCount} distinct ready attachments.");
        var access = await GetActiveConversationAccessAsync(actorUserId, conversationId, cancellationToken);
        if (!access.Succeeded) return ApplicationResult<MessageResponse>.Failure(access.Error!);
        var conversation = access.Value!.Conversation;
        var relationshipError = await ValidateConversationRelationshipAsync(conversation, actorUserId, cancellationToken);
        if (relationshipError is not null) return ApplicationResult<MessageResponse>.Failure(relationshipError);
        if (request.ReplyToMessageId is not null)
        {
            var replyTo = await dbContext.Messages.AsNoTracking().SingleOrDefaultAsync(message => message.Id == request.ReplyToMessageId.Value && message.ConversationId == conversationId, cancellationToken);
            if (replyTo is null) return Failure<MessageResponse>("invalid_reply", "A reply must reference a message in the same conversation.");
            var replyVisibilityError = await ValidateMessageVisibilityAsync(actorUserId, replyTo, cancellationToken);
            if (replyVisibilityError is not null) return ApplicationResult<MessageResponse>.Failure(replyVisibilityError);
        }
        if (attachmentIds.Length > 0)
        {
            var media = await mediaService.ValidatePostMediaAsync(actorUserId, attachmentIds, cancellationToken);
            if (!media.Succeeded) return ApplicationResult<MessageResponse>.Failure(ToMessagesError(media.Error!));
        }
        var now = timeProvider.GetUtcNow();
        var message = Message.Create(Guid.NewGuid(), conversationId, actorUserId, attachmentIds.Length == 0 ? MessageType.Text : MessageType.Media,
            string.IsNullOrWhiteSpace(content) ? null : content, request.ReplyToMessageId, now, storyId);
        conversation.RecordMessage(now);
        dbContext.Messages.Add(message);
        dbContext.MessageAttachments.AddRange(attachmentIds.Select((mediaId, index) => MessageAttachment.Create(message.Id, mediaId, index)));
        var recipients = await GetVisibleRecipientUserIdsAsync(conversation, actorUserId, cancellationToken);
        var recipientParticipants = await dbContext.ConversationParticipants.Where(participant => participant.ConversationId == conversationId && participant.LeftAtUtc == null && recipients.Contains(participant.UserId)).ToListAsync(cancellationToken);
        foreach (var recipient in recipientParticipants)
        {
            recipient.MarkDelivered(message.Id);
            dbContext.MessageNotifications.Add(MessageNotification.Create(Guid.NewGuid(), recipient.UserId, conversationId, message.Id, now));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        var actorBlockedUserIds = (await friendsService.GetAccessSnapshotAsync(actorUserId, cancellationToken)).BlockedUserIds;
        var response = (await BuildMessageResponsesAsync([message], actorUserId, actorBlockedUserIds, cancellationToken))[0];
        foreach (var recipient in recipients)
        {
            var recipientBlockedUserIds = (await friendsService.GetAccessSnapshotAsync(recipient, cancellationToken)).BlockedUserIds;
            var recipientResponse = (await BuildMessageResponsesAsync([message], recipient, recipientBlockedUserIds, cancellationToken))[0];
            var sidebar = await BuildConversationResponseAsync(conversation, recipient, cancellationToken);
            await hubContext.Clients.User(recipient.ToString()).SendAsync("MessageReceived", new IncomingMessageResponse(sidebar, recipientResponse), cancellationToken);
            await hubContext.Clients.User(recipient.ToString()).SendAsync("MessageCreated", recipientResponse, cancellationToken);
        }
        return ApplicationResult<MessageResponse>.Success(response);
    }

    public async Task<ApplicationResult<MessageResponse>> EditMessageAsync(Guid actorUserId, Guid messageId, EditMessageRequest request, CancellationToken cancellationToken = default)
    {
        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content.Length > MaximumContentLength) return Failure<MessageResponse>("invalid_message_content", $"Message content must contain between 1 and {MaximumContentLength} characters.");
        var message = await dbContext.Messages.SingleOrDefaultAsync(item => item.Id == messageId, cancellationToken);
        if (message is null) return Failure<MessageResponse>("message_not_found", "The message was not found.", ApplicationErrorType.NotFound);
        var access = await GetActiveConversationAccessAsync(actorUserId, message.ConversationId, cancellationToken);
        if (!access.Succeeded) return ApplicationResult<MessageResponse>.Failure(access.Error!);
        if (message.SenderUserId != actorUserId) return Forbidden<MessageResponse>();
        try { message.Edit(content, timeProvider.GetUtcNow()); }
        catch (InvalidOperationException) { return Conflict<MessageResponse>("message_cannot_be_edited", "Only active text messages can be edited."); }
        await dbContext.SaveChangesAsync(cancellationToken);
        var actorBlockedUserIds = (await friendsService.GetAccessSnapshotAsync(actorUserId, cancellationToken)).BlockedUserIds;
        var response = (await BuildMessageResponsesAsync([message], actorUserId, actorBlockedUserIds, cancellationToken))[0];
        await NotifyMessageChangedAsync(access.Value!.Conversation, actorUserId, "MessageEdited", message, cancellationToken);
        return ApplicationResult<MessageResponse>.Success(response);
    }

    public async Task<ApplicationResult<bool>> DeleteMessageAsync(Guid actorUserId, Guid messageId, CancellationToken cancellationToken = default)
    {
        var message = await dbContext.Messages.SingleOrDefaultAsync(item => item.Id == messageId, cancellationToken);
        if (message is null) return Failure<bool>("message_not_found", "The message was not found.", ApplicationErrorType.NotFound);
        var access = await GetActiveConversationAccessAsync(actorUserId, message.ConversationId, cancellationToken);
        if (!access.Succeeded) return ApplicationResult<bool>.Failure(access.Error!);
        if (message.SenderUserId != actorUserId) return Forbidden<bool>();
        message.Delete(timeProvider.GetUtcNow());
        await dbContext.MessageAttachments.Where(attachment => attachment.MessageId == messageId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.MessageReactions.Where(reaction => reaction.MessageId == messageId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyActiveParticipantsAsync(access.Value!.Conversation, actorUserId, "MessageDeleted", new { MessageId = message.Id, ConversationId = message.ConversationId, DeletedAtUtc = message.DeletedAtUtc }, cancellationToken);
        return ApplicationResult<bool>.Success(true);
    }

    public async Task<ApplicationResult<MessageReactionResponse>> SetReactionAsync(Guid actorUserId, Guid messageId, SetMessageReactionRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<MessageReactionType>(request.Type, true, out var type) || !Enum.IsDefined(type)) return Failure<MessageReactionResponse>("invalid_reaction", "Reaction type must be one of: like, love, haha, wow, sad, angry.");
        var message = await dbContext.Messages.SingleOrDefaultAsync(item => item.Id == messageId, cancellationToken);
        if (message is null || message.DeletedAtUtc is not null) return Failure<MessageReactionResponse>("message_not_found", "The message was not found.", ApplicationErrorType.NotFound);
        var access = await GetActiveConversationAccessAsync(actorUserId, message.ConversationId, cancellationToken);
        if (!access.Succeeded) return ApplicationResult<MessageReactionResponse>.Failure(access.Error!);
        var relationshipError = await ValidateConversationRelationshipAsync(access.Value!.Conversation, actorUserId, cancellationToken);
        if (relationshipError is not null) return ApplicationResult<MessageReactionResponse>.Failure(relationshipError);
        var visibilityError = await ValidateMessageVisibilityAsync(actorUserId, message, cancellationToken);
        if (visibilityError is not null) return ApplicationResult<MessageReactionResponse>.Failure(visibilityError);
        var reaction = await dbContext.MessageReactions.SingleOrDefaultAsync(item => item.MessageId == messageId && item.UserId == actorUserId, cancellationToken);
        if (reaction is null) { reaction = MessageReaction.Create(messageId, actorUserId, type, timeProvider.GetUtcNow()); dbContext.MessageReactions.Add(reaction); }
        else reaction.ChangeTo(type, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        var response = ToReactionResponse(reaction);
        var recipients = await GetVisibleRecipientUserIdsForMessageAsync(access.Value.Conversation, actorUserId, message.SenderUserId, cancellationToken);
        if (recipients.Length > 0) await hubContext.Clients.Users(recipients.Select(userId => userId.ToString())).SendAsync("MessageReactionChanged", new { MessageId = messageId, Reaction = response }, cancellationToken);
        return ApplicationResult<MessageReactionResponse>.Success(response);
    }

    public async Task<ApplicationResult<bool>> RemoveReactionAsync(Guid actorUserId, Guid messageId, CancellationToken cancellationToken = default)
    {
        var message = await dbContext.Messages.AsNoTracking().SingleOrDefaultAsync(item => item.Id == messageId, cancellationToken);
        if (message is null || message.DeletedAtUtc is not null) return Failure<bool>("message_not_found", "The message was not found.", ApplicationErrorType.NotFound);
        var access = await GetActiveConversationAccessAsync(actorUserId, message.ConversationId, cancellationToken);
        if (!access.Succeeded) return ApplicationResult<bool>.Failure(access.Error!);
        var relationshipError = await ValidateConversationRelationshipAsync(access.Value!.Conversation, actorUserId, cancellationToken);
        if (relationshipError is not null) return ApplicationResult<bool>.Failure(relationshipError);
        var visibilityError = await ValidateMessageVisibilityAsync(actorUserId, message, cancellationToken);
        if (visibilityError is not null) return ApplicationResult<bool>.Failure(visibilityError);
        var reaction = await dbContext.MessageReactions.SingleOrDefaultAsync(item => item.MessageId == messageId && item.UserId == actorUserId, cancellationToken);
        if (reaction is not null) { dbContext.MessageReactions.Remove(reaction); await dbContext.SaveChangesAsync(cancellationToken); }
        var recipients = await GetVisibleRecipientUserIdsForMessageAsync(access.Value!.Conversation, actorUserId, message.SenderUserId, cancellationToken);
        if (recipients.Length > 0) await hubContext.Clients.Users(recipients.Select(userId => userId.ToString())).SendAsync("MessageReactionChanged", new { MessageId = messageId, RemovedUserId = actorUserId }, cancellationToken);
        return ApplicationResult<bool>.Success(true);
    }

    public async Task<ApplicationResult<ConversationResponse>> AddParticipantsAsync(Guid actorUserId, Guid conversationId, AddConversationParticipantsRequest request, CancellationToken cancellationToken = default)
    {
        var userIds = request.UserIds?.ToArray() ?? [];
        if (userIds.Length is < 1 or > MaximumGroupSize || userIds.Any(id => id == Guid.Empty) || userIds.Distinct().Count() != userIds.Length) return Failure<ConversationResponse>("invalid_group_participants", "Participant IDs must be distinct and non-empty.");
        var access = await GetActiveConversationAccessAsync(actorUserId, conversationId, cancellationToken);
        if (!access.Succeeded) return ApplicationResult<ConversationResponse>.Failure(access.Error!);
        var (conversation, actor) = access.Value!;
        if (conversation.Type != ConversationType.Group || !CanManageConversation(actor.Role)) return Forbidden<ConversationResponse>();
        var existing = await dbContext.ConversationParticipants.Where(participant => participant.ConversationId == conversationId).ToListAsync(cancellationToken);
        var newIds = userIds.Except(existing.Where(participant => participant.LeftAtUtc is null).Select(participant => participant.UserId)).ToArray();
        if (existing.Count(participant => participant.LeftAtUtc is null) + newIds.Length > MaximumGroupSize) return Failure<ConversationResponse>("group_size_limit", $"A group can contain at most {MaximumGroupSize} participants.");
        if (await dbContext.Users.AsNoTracking().CountAsync(user => newIds.Contains(user.Id), cancellationToken) != newIds.Length) return Failure<ConversationResponse>("group_participant_not_found", "Every group participant must be an existing user.", ApplicationErrorType.NotFound);
        var now = timeProvider.GetUtcNow();
        var latest = await dbContext.Messages.AsNoTracking().Where(message => message.ConversationId == conversationId).OrderByDescending(message => message.CreatedAtUtc).ThenByDescending(message => message.Id).FirstOrDefaultAsync(cancellationToken);
        foreach (var userId in newIds)
        {
            var target = existing.SingleOrDefault(participant => participant.UserId == userId);
            if (target is null) { target = ConversationParticipant.Create(conversationId, userId, ConversationParticipantRole.Member, now); dbContext.ConversationParticipants.Add(target); }
            else target.Rejoin(ConversationParticipantRole.Member, now);
            if (latest is not null) target.AdvanceReadCursor(latest.Id, latest.CreatedAtUtc, now);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        var response = await BuildConversationResponseAsync(conversation, actorUserId, cancellationToken);
        await hubContext.Clients.Users(newIds.Select(id => id.ToString())).SendAsync("ParticipantAdded", new { ConversationId = conversationId, UserIds = newIds }, cancellationToken);
        await NotifyActiveParticipantsAsync(conversation, actorUserId, "ConversationUpdated", response, cancellationToken);
        return ApplicationResult<ConversationResponse>.Success(response);
    }

    public async Task<ApplicationResult<bool>> RemoveParticipantAsync(Guid actorUserId, Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var access = await GetActiveConversationAccessAsync(actorUserId, conversationId, cancellationToken);
        if (!access.Succeeded) return ApplicationResult<bool>.Failure(access.Error!);
        var (conversation, actor) = access.Value!;
        if (conversation.Type != ConversationType.Group) return Failure<bool>("invalid_conversation_type", "Only group conversations have removable participants.");
        var target = await dbContext.ConversationParticipants.SingleOrDefaultAsync(participant => participant.ConversationId == conversationId && participant.UserId == userId && participant.LeftAtUtc == null, cancellationToken);
        if (target is null) return Failure<bool>("participant_not_found", "The participant is not active in this conversation.", ApplicationErrorType.NotFound);
        if (!CanRemoveParticipant(actor.Role, target.Role, actorUserId == userId)) return Forbidden<bool>();
        target.Leave(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        await hubContext.Clients.User(userId.ToString()).SendAsync("ParticipantRemoved", new { ConversationId = conversationId, UserId = userId }, cancellationToken);
        await NotifyActiveParticipantsAsync(conversation, actorUserId, "ParticipantRemoved", new { ConversationId = conversationId, UserId = userId }, cancellationToken);
        return ApplicationResult<bool>.Success(true);
    }

    public async Task<ApplicationResult<bool>> ChangeParticipantRoleAsync(Guid actorUserId, Guid conversationId, Guid userId, ChangeConversationParticipantRoleRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<ConversationParticipantRole>(request.Role, true, out var role) || !Enum.IsDefined(role) || role == ConversationParticipantRole.Owner) return Failure<bool>("invalid_participant_role", "Role must be admin or member.");
        var access = await GetActiveConversationAccessAsync(actorUserId, conversationId, cancellationToken);
        if (!access.Succeeded) return ApplicationResult<bool>.Failure(access.Error!);
        var (conversation, actor) = access.Value!;
        if (conversation.Type != ConversationType.Group || actor.Role != ConversationParticipantRole.Owner) return Forbidden<bool>();
        var target = await dbContext.ConversationParticipants.SingleOrDefaultAsync(participant => participant.ConversationId == conversationId && participant.UserId == userId && participant.LeftAtUtc == null, cancellationToken);
        if (target is null) return Failure<bool>("participant_not_found", "The participant is not active in this conversation.", ApplicationErrorType.NotFound);
        if (target.Role == ConversationParticipantRole.Owner) return Conflict<bool>("owner_role_protected", "Transfer ownership before changing the owner's role.");
        target.ChangeRole(role);
        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyActiveParticipantsAsync(conversation, actorUserId, "ConversationUpdated", new { ConversationId = conversationId, UserId = userId, Role = role.ToString().ToLowerInvariant() }, cancellationToken);
        return ApplicationResult<bool>.Success(true);
    }

    public async Task<ApplicationResult<bool>> LeaveConversationAsync(Guid actorUserId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        var access = await GetActiveConversationAccessAsync(actorUserId, conversationId, cancellationToken);
        if (!access.Succeeded) return ApplicationResult<bool>.Failure(access.Error!);
        var (conversation, participant) = access.Value!;
        if (conversation.Type != ConversationType.Group) return Failure<bool>("invalid_conversation_type", "Direct conversation participants cannot leave the conversation.");
        if (participant.Role == ConversationParticipantRole.Owner) return Conflict<bool>("owner_must_transfer", "Transfer ownership before leaving this group conversation.");
        participant.Leave(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyActiveParticipantsAsync(conversation, actorUserId, "ParticipantRemoved", new { ConversationId = conversationId, UserId = actorUserId }, cancellationToken);
        return ApplicationResult<bool>.Success(true);
    }

    public async Task<ApplicationResult<bool>> TransferOwnershipAsync(Guid actorUserId, Guid conversationId, TransferConversationOwnershipRequest request, CancellationToken cancellationToken = default)
    {
        var access = await GetActiveConversationAccessAsync(actorUserId, conversationId, cancellationToken);
        if (!access.Succeeded) return ApplicationResult<bool>.Failure(access.Error!);
        var (conversation, owner) = access.Value!;
        if (conversation.Type != ConversationType.Group || owner.Role != ConversationParticipantRole.Owner) return Forbidden<bool>();
        var target = await dbContext.ConversationParticipants.SingleOrDefaultAsync(participant => participant.ConversationId == conversationId && participant.UserId == request.UserId && participant.LeftAtUtc == null, cancellationToken);
        if (target is null || target.UserId == actorUserId) return Failure<bool>("participant_not_found", "Ownership must be transferred to another active participant.");
        owner.ChangeRole(ConversationParticipantRole.Admin);
        target.ChangeRole(ConversationParticipantRole.Owner);
        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyActiveParticipantsAsync(conversation, actorUserId, "ConversationUpdated", new { ConversationId = conversationId, OwnerUserId = target.UserId }, cancellationToken);
        return ApplicationResult<bool>.Success(true);
    }

    public async Task<ApplicationResult<IReadOnlyList<MessageResponse>>> SearchMessagesAsync(Guid actorUserId, Guid conversationId, string? queryText, CancellationToken cancellationToken = default)
    {
        var query = queryText?.Trim();
        if (string.IsNullOrWhiteSpace(query) || query.Length > 200) return Failure<IReadOnlyList<MessageResponse>>("invalid_search_query", "Search text must contain between 1 and 200 characters.");
        var access = await GetActiveConversationAccessAsync(actorUserId, conversationId, cancellationToken);
        if (!access.Succeeded) return ApplicationResult<IReadOnlyList<MessageResponse>>.Failure(access.Error!);
        var relationshipError = await ValidateConversationRelationshipAsync(access.Value!.Conversation, actorUserId, cancellationToken);
        if (relationshipError is not null) return ApplicationResult<IReadOnlyList<MessageResponse>>.Failure(relationshipError);
        var blocked = (await friendsService.GetAccessSnapshotAsync(actorUserId, cancellationToken)).BlockedUserIds;
        var messages = await dbContext.Messages.AsNoTracking().Where(message => message.ConversationId == conversationId && message.DeletedAtUtc == null && message.Content != null && !blocked.Contains(message.SenderUserId) && EF.Functions.ILike(message.Content, $"%{query}%"))
            .OrderByDescending(message => message.CreatedAtUtc).ThenByDescending(message => message.Id).Take(50).ToListAsync(cancellationToken);
        return ApplicationResult<IReadOnlyList<MessageResponse>>.Success(await BuildMessageResponsesAsync(messages, actorUserId, blocked, cancellationToken));
    }

    public async Task<ApplicationResult<MediaReadUrlResponse>> CreateMessageMediaReadUrlAsync(Guid actorUserId, Guid mediaId, CancellationToken cancellationToken = default)
    {
        var blockedUserIds = (await friendsService.GetAccessSnapshotAsync(actorUserId, cancellationToken)).BlockedUserIds;
        var candidates = await (from participant in dbContext.ConversationParticipants.AsNoTracking()
                                join conversation in dbContext.Conversations.AsNoTracking() on participant.ConversationId equals conversation.Id
                                where participant.UserId == actorUserId && participant.LeftAtUtc == null &&
                                      (conversation.PhotoMediaId == mediaId || dbContext.MessageAttachments.Any(attachment => attachment.MediaId == mediaId && dbContext.Messages.Any(message => message.Id == attachment.MessageId && message.ConversationId == conversation.Id && message.DeletedAtUtc == null && !blockedUserIds.Contains(message.SenderUserId))))
                                select conversation).Distinct().ToListAsync(cancellationToken);
        var authorized = false;
        foreach (var conversation in candidates)
        {
            if (await ValidateConversationRelationshipAsync(conversation, actorUserId, cancellationToken) is null)
            {
                authorized = true;
                break;
            }
        }
        if (!authorized) return Failure<MediaReadUrlResponse>("media_access_denied", "You do not have access to this conversation media.", ApplicationErrorType.Forbidden);
        var result = await mediaService.CreateReadUrlAsync(mediaId, cancellationToken);
        return result.Succeeded ? ApplicationResult<MediaReadUrlResponse>.Success(result.Value!) : ApplicationResult<MediaReadUrlResponse>.Failure(ToMessagesError(result.Error!));
    }

    public async Task<ApplicationResult<PagedResponse<IncomingMessageResponse>>> GetUnreadNotificationsAsync(Guid actorUserId, int offset, int limit, CancellationToken cancellationToken = default)
    {
        if (offset < 0 || limit is < 1 or > MaximumLimit) return Failure<PagedResponse<IncomingMessageResponse>>("invalid_pagination", $"Offset must be non-negative and limit must be between 1 and {MaximumLimit}.");
        var accessSnapshot = await friendsService.GetAccessSnapshotAsync(actorUserId, cancellationToken);
        var blocked = accessSnapshot.BlockedUserIds;
        var permittedDirectUsers = accessSnapshot.FriendUserIds.Except(blocked).ToArray();
        var query = from notification in dbContext.MessageNotifications.AsNoTracking()
                    join message in dbContext.Messages.AsNoTracking() on notification.MessageId equals message.Id
                    join participant in dbContext.ConversationParticipants.AsNoTracking() on new { notification.ConversationId, UserId = actorUserId } equals new { participant.ConversationId, participant.UserId }
                    join conversation in dbContext.Conversations.AsNoTracking() on notification.ConversationId equals conversation.Id
                    where notification.RecipientUserId == actorUserId && notification.ReadAtUtc == null && participant.LeftAtUtc == null && !blocked.Contains(message.SenderUserId) && message.DeletedAtUtc == null &&
                          (conversation.Type != ConversationType.Direct ||
                           dbContext.ConversationParticipants.Any(peer => peer.ConversationId == conversation.Id && peer.UserId != actorUserId && peer.LeftAtUtc == null && permittedDirectUsers.Contains(peer.UserId)))
                    select notification;
        var total = await query.CountAsync(cancellationToken);
        var notifications = await query.OrderByDescending(notification => notification.CreatedAtUtc).Skip(offset).Take(limit).ToListAsync(cancellationToken);
        var conversationIds = notifications.Select(notification => notification.ConversationId).Distinct().ToArray();
        var messageIds = notifications.Select(notification => notification.MessageId).Distinct().ToArray();
        var conversations = await dbContext.Conversations.AsNoTracking().Where(conversation => conversationIds.Contains(conversation.Id)).ToDictionaryAsync(conversation => conversation.Id, cancellationToken);
        var messageMap = (await dbContext.Messages.AsNoTracking().Where(message => messageIds.Contains(message.Id)).ToListAsync(cancellationToken)).ToDictionary(message => message.Id);
        var participants = await LoadParticipantMapAsync(conversationIds, cancellationToken);
        var unread = await GetUnreadCountsAsync(actorUserId, conversationIds, blocked, cancellationToken);
        var responses = (await BuildMessageResponsesAsync(messageMap.Values, actorUserId, blocked, cancellationToken)).ToDictionary(message => message.Id);
        var items = notifications.Where(notification => conversations.ContainsKey(notification.ConversationId) && responses.ContainsKey(notification.MessageId)).Select(notification => new IncomingMessageResponse(
            ToConversationResponse(conversations[notification.ConversationId], actorUserId, participants[notification.ConversationId].Single(participant => participant.UserId == actorUserId), messageMap[notification.MessageId], unread.GetValueOrDefault(notification.ConversationId), participants[notification.ConversationId]), responses[notification.MessageId])).ToArray();
        return ApplicationResult<PagedResponse<IncomingMessageResponse>>.Success(new PagedResponse<IncomingMessageResponse>(items, offset, limit, total));
    }

    public async Task NotifyTypingAsync(Guid actorUserId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        var access = await GetActiveConversationAccessAsync(actorUserId, conversationId, cancellationToken);
        if (!access.Succeeded || await ValidateConversationRelationshipAsync(access.Value!.Conversation, actorUserId, cancellationToken) is not null) return;
        var typing = new MessageTypingResponse(conversationId, actorUserId);
        await NotifyActiveParticipantsAsync(access.Value.Conversation, actorUserId, "TypingChanged", new { typing.ConversationId, typing.SenderUserId, IsTyping = true }, cancellationToken);
        await NotifyActiveParticipantsAsync(access.Value.Conversation, actorUserId, "TypingStarted", typing, cancellationToken);
    }

    private async Task<ApplicationResult<(Conversation Conversation, ConversationParticipant Participant)>> GetActiveConversationAccessAsync(Guid actorUserId, Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations.SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken);
        if (conversation is null) return Failure<(Conversation, ConversationParticipant)>("conversation_not_found", "Conversation was not found.", ApplicationErrorType.NotFound);
        var participant = await dbContext.ConversationParticipants.SingleOrDefaultAsync(item => item.ConversationId == conversationId && item.UserId == actorUserId && item.LeftAtUtc == null, cancellationToken);
        return participant is null ? Forbidden<(Conversation, ConversationParticipant)>() : ApplicationResult<(Conversation, ConversationParticipant)>.Success((conversation, participant));
    }

    private async Task<Conversation?> FindDirectConversationAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken)
    {
        var (userId1, userId2) = firstUserId.CompareTo(secondUserId) < 0 ? (firstUserId, secondUserId) : (secondUserId, firstUserId);
        return await dbContext.Conversations.SingleOrDefaultAsync(conversation => conversation.Type == ConversationType.Direct && conversation.UserId1 == userId1 && conversation.UserId2 == userId2, cancellationToken);
    }

    private async Task<ApplicationError?> ValidateConversationRelationshipAsync(Conversation conversation, Guid actorUserId, CancellationToken cancellationToken)
    {
        if (conversation.Type != ConversationType.Direct) return null;
        var otherUserId = await dbContext.ConversationParticipants.AsNoTracking()
            .Where(participant => participant.ConversationId == conversation.Id && participant.UserId != actorUserId && participant.LeftAtUtc == null)
            .Select(participant => (Guid?)participant.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        return otherUserId is null
            ? new ApplicationError("conversation_access_denied", "You do not have access to this conversation.", ApplicationErrorType.Forbidden)
            : await ValidateDirectRelationshipAsync(actorUserId, otherUserId.Value, cancellationToken);
    }

    private async Task<ApplicationError?> ValidateDirectRelationshipAsync(Guid actorUserId, Guid otherUserId, CancellationToken cancellationToken)
    {
        var snapshot = await friendsService.GetAccessSnapshotAsync(actorUserId, cancellationToken);
        if (snapshot.BlockedUserIds.Contains(otherUserId)) return new ApplicationError("messaging_blocked", "Messaging is unavailable because one of you has blocked the other.", ApplicationErrorType.Forbidden);
        return snapshot.FriendUserIds.Contains(otherUserId) ? null : new ApplicationError("messaging_requires_friendship", "You can only message friends.", ApplicationErrorType.Forbidden);
    }

    private async Task<ApplicationError?> ValidateMessageVisibilityAsync(Guid actorUserId, Message message, CancellationToken cancellationToken)
    {
        if (message.SenderUserId == actorUserId) return null;
        var blockedUserIds = (await friendsService.GetAccessSnapshotAsync(actorUserId, cancellationToken)).BlockedUserIds;
        return blockedUserIds.Contains(message.SenderUserId)
            ? new ApplicationError("message_access_denied", "You do not have access to this message.", ApplicationErrorType.Forbidden)
            : null;
    }

    private async Task<ConversationResponse> BuildConversationResponseAsync(Conversation conversation, Guid actorUserId, CancellationToken cancellationToken)
    {
        var actor = await dbContext.ConversationParticipants.AsNoTracking().SingleAsync(item => item.ConversationId == conversation.Id && item.UserId == actorUserId, cancellationToken);
        var participants = await dbContext.ConversationParticipants.AsNoTracking().Where(item => item.ConversationId == conversation.Id && item.LeftAtUtc == null).ToListAsync(cancellationToken);
        var blocked = (await friendsService.GetAccessSnapshotAsync(actorUserId, cancellationToken)).BlockedUserIds;
        var latest = (await LoadLatestMessagesAsync([conversation.Id], blocked, cancellationToken)).GetValueOrDefault(conversation.Id);
        var unread = (await GetUnreadCountsAsync(actorUserId, [conversation.Id], blocked, cancellationToken)).GetValueOrDefault(conversation.Id);
        return ToConversationResponse(conversation, actorUserId, actor, latest, unread, participants);
    }

    private async Task<Dictionary<Guid, IReadOnlyList<ConversationParticipant>>> LoadParticipantMapAsync(IReadOnlyCollection<Guid> conversationIds, CancellationToken cancellationToken)
    {
        if (conversationIds.Count == 0) return [];
        var participants = await dbContext.ConversationParticipants.AsNoTracking().Where(item => conversationIds.Contains(item.ConversationId) && item.LeftAtUtc == null).ToListAsync(cancellationToken);
        return participants.GroupBy(item => item.ConversationId).ToDictionary(group => group.Key, group => (IReadOnlyList<ConversationParticipant>)group.ToArray());
    }

    private async Task<Dictionary<Guid, Message>> LoadLatestMessagesAsync(IReadOnlyCollection<Guid> conversationIds, IReadOnlySet<Guid> blockedUserIds, CancellationToken cancellationToken)
    {
        if (conversationIds.Count == 0) return [];
        var messages = await dbContext.Messages.AsNoTracking().Where(message => conversationIds.Contains(message.ConversationId) && !blockedUserIds.Contains(message.SenderUserId))
            .GroupBy(message => message.ConversationId).Select(group => group.OrderByDescending(message => message.CreatedAtUtc).ThenByDescending(message => message.Id).First()).ToListAsync(cancellationToken);
        return messages.ToDictionary(message => message.ConversationId);
    }

    private async Task<Dictionary<Guid, int>> GetUnreadCountsAsync(Guid actorUserId, IReadOnlyCollection<Guid> conversationIds, IReadOnlySet<Guid> blockedUserIds, CancellationToken cancellationToken)
    {
        if (conversationIds.Count == 0) return [];
        return await (from message in dbContext.Messages.AsNoTracking()
                      join participant in dbContext.ConversationParticipants.AsNoTracking() on message.ConversationId equals participant.ConversationId
                      where participant.UserId == actorUserId && participant.LeftAtUtc == null && conversationIds.Contains(message.ConversationId) && message.SenderUserId != actorUserId && message.DeletedAtUtc == null && !blockedUserIds.Contains(message.SenderUserId) &&
                            (participant.LastReadMessageCreatedAtUtc == null || participant.LastReadMessageId == null || message.CreatedAtUtc > participant.LastReadMessageCreatedAtUtc ||
                             (message.CreatedAtUtc == participant.LastReadMessageCreatedAtUtc && message.Id.CompareTo(participant.LastReadMessageId.Value) > 0))
                      group message by message.ConversationId into groupByConversation
                      select new { ConversationId = groupByConversation.Key, Count = groupByConversation.Count() }).ToDictionaryAsync(item => item.ConversationId, item => item.Count, cancellationToken);
    }

    private async Task<IReadOnlyList<MessageResponse>> BuildMessageResponsesAsync(
        IEnumerable<Message> source,
        Guid viewerUserId,
        IReadOnlySet<Guid> blockedUserIds,
        CancellationToken cancellationToken)
    {
        var messages = source.ToArray();
        if (messages.Length == 0) return [];
        var ids = messages.Select(message => message.Id).ToArray();
        var replyIds = messages.Where(message => message.ReplyToMessageId is not null).Select(message => message.ReplyToMessageId!.Value).Distinct().ToArray();
        var attachments = await dbContext.MessageAttachments.AsNoTracking().Where(attachment => ids.Contains(attachment.MessageId)).ToListAsync(cancellationToken);
        var reactions = await dbContext.MessageReactions.AsNoTracking().Where(reaction => ids.Contains(reaction.MessageId) && !blockedUserIds.Contains(reaction.UserId)).ToListAsync(cancellationToken);
        var replies = replyIds.Length == 0 ? [] : await dbContext.Messages.AsNoTracking().Where(message => replyIds.Contains(message.Id) && !blockedUserIds.Contains(message.SenderUserId)).ToDictionaryAsync(message => message.Id, cancellationToken);
        var stories = await LoadStoryReferencesAsync(messages, viewerUserId, blockedUserIds, cancellationToken);
        return messages.Select(message => ToMessageResponse(message, replies.GetValueOrDefault(message.ReplyToMessageId ?? Guid.Empty), attachments.Where(attachment => attachment.MessageId == message.Id), reactions.Where(reaction => reaction.MessageId == message.Id), stories.GetValueOrDefault(message.Id))).ToArray();
    }

    private async Task<Dictionary<Guid, MessageStoryReferenceResponse>> LoadStoryReferencesAsync(
        IReadOnlyCollection<Message> messages,
        Guid viewerUserId,
        IReadOnlySet<Guid> blockedUserIds,
        CancellationToken cancellationToken)
    {
        var references = messages
            .Where(message => message.DeletedAtUtc is null && message.StoryId is not null)
            .Select(message => new { message.Id, StoryId = message.StoryId!.Value })
            .ToArray();
        if (references.Length == 0)
        {
            return [];
        }

        var storyIds = references.Select(reference => reference.StoryId).Distinct().ToArray();
        var now = timeProvider.GetUtcNow();
        var stories = await dbContext.Stories.AsNoTracking()
            .Where(story => storyIds.Contains(story.Id) && story.DeletedAtUtc == null &&
                            story.ExpiresAtUtc > now)
            .ToDictionaryAsync(story => story.Id, cancellationToken);
        var snapshot = await friendsService.GetAccessSnapshotAsync(viewerUserId, cancellationToken);
        var mediaIds = stories.Values.Select(story => story.MediaId).Distinct().ToArray();
        var mediaTypes = mediaIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.MediaAssets.AsNoTracking()
                .Where(asset => mediaIds.Contains(asset.Id) && asset.Status == MediaStatus.Ready &&
                                asset.DeletedAtUtc == null)
                .Select(asset => new { asset.Id, asset.MediaType })
                .ToDictionaryAsync(
                    item => item.Id,
                    item => item.MediaType.ToString().ToLowerInvariant(),
                    cancellationToken);
        var result = new Dictionary<Guid, MessageStoryReferenceResponse>();
        foreach (var reference in references)
        {
            if (!stories.TryGetValue(reference.StoryId, out var story) || story is null ||
                !mediaTypes.TryGetValue(story.MediaId, out var mediaType))
            {
                result[reference.Id] = new MessageStoryReferenceResponse(reference.StoryId, false, null, null);
                continue;
            }

            var available = story.AuthorUserId == viewerUserId ||
                (!blockedUserIds.Contains(story.AuthorUserId) &&
                 (story.Privacy == PostPrivacy.Public ||
                  (story.Privacy == PostPrivacy.Friends &&
                   snapshot.FriendUserIds.Contains(story.AuthorUserId))));
            result[reference.Id] = available
                ? new MessageStoryReferenceResponse(story.Id, true, story.Caption, mediaType)
                : new MessageStoryReferenceResponse(story.Id, false, null, null);
        }

        return result;
    }

    private async Task<Guid[]> GetVisibleRecipientUserIdsAsync(Conversation conversation, Guid senderUserId, CancellationToken cancellationToken)
    {
        var recipients = await dbContext.ConversationParticipants.AsNoTracking().Where(participant => participant.ConversationId == conversation.Id && participant.LeftAtUtc == null && participant.UserId != senderUserId).Select(participant => participant.UserId).ToArrayAsync(cancellationToken);
        // Group membership survives blocks, while Direct conversations may remain in storage after a block.
        // In both cases a block filters realtime delivery for that pair.
        var blocked = (await friendsService.GetAccessSnapshotAsync(senderUserId, cancellationToken)).BlockedUserIds;
        return recipients.Where(userId => !blocked.Contains(userId)).ToArray();
    }

    private async Task<Guid[]> GetVisibleRecipientUserIdsForMessageAsync(Conversation conversation, Guid senderUserId, Guid messageSenderUserId, CancellationToken cancellationToken)
    {
        var recipients = await GetVisibleRecipientUserIdsAsync(conversation, senderUserId, cancellationToken);
        if (recipients.Length == 0) return recipients;
        var visibleRecipients = new List<Guid>(recipients.Length);
        foreach (var recipient in recipients)
        {
            var blockedUserIds = (await friendsService.GetAccessSnapshotAsync(recipient, cancellationToken)).BlockedUserIds;
            if (!blockedUserIds.Contains(messageSenderUserId)) visibleRecipients.Add(recipient);
        }
        return visibleRecipients.ToArray();
    }

    private async Task NotifyMessageChangedAsync(Conversation conversation, Guid actorUserId, string eventName, Message message, CancellationToken cancellationToken)
    {
        var recipients = await GetVisibleRecipientUserIdsForMessageAsync(conversation, actorUserId, message.SenderUserId, cancellationToken);
        foreach (var recipient in recipients)
        {
            var blockedUserIds = (await friendsService.GetAccessSnapshotAsync(recipient, cancellationToken)).BlockedUserIds;
            var response = (await BuildMessageResponsesAsync([message], recipient, blockedUserIds, cancellationToken))[0];
            await hubContext.Clients.User(recipient.ToString()).SendAsync(eventName, response, cancellationToken);
        }
    }

    private async Task NotifyActiveParticipantsAsync(Conversation conversation, Guid actorUserId, string eventName, object payload, CancellationToken cancellationToken)
    {
        var recipients = await GetVisibleRecipientUserIdsAsync(conversation, actorUserId, cancellationToken);
        if (recipients.Length > 0) await hubContext.Clients.Users(recipients.Select(userId => userId.ToString())).SendAsync(eventName, payload, cancellationToken);
    }

    private static ConversationResponse ToConversationResponse(Conversation conversation, Guid actorUserId, ConversationParticipant actor, Message? lastMessage, int unreadCount, IReadOnlyList<ConversationParticipant> participants) => new(
        conversation.Id, conversation.Type == ConversationType.Direct ? participants.SingleOrDefault(participant => participant.UserId != actorUserId)?.UserId : null, conversation.CreatedAtUtc, conversation.LastMessageAtUtc,
        lastMessage is null ? null : ToMessageResponse(lastMessage, null, [], []), unreadCount, conversation.Type.ToString().ToLowerInvariant(), conversation.Title, conversation.PhotoMediaId,
        participants.Select(ToParticipantResponse).ToArray(), actor.MutedUntilUtc is not null && actor.MutedUntilUtc > DateTimeOffset.UtcNow, actor.ArchivedAtUtc is not null);
    private static ConversationParticipantResponse ToParticipantResponse(ConversationParticipant participant) => new(participant.UserId, participant.Role.ToString().ToLowerInvariant(), participant.JoinedAtUtc, participant.LeftAtUtc, participant.LastReadMessageId, participant.LastReadAtUtc, participant.LastDeliveredMessageId, participant.Nickname);
    private static MessageResponse ToMessageResponse(Message message, Message? replyTo, IEnumerable<MessageAttachment> attachments, IEnumerable<MessageReaction> reactions, MessageStoryReferenceResponse? story = null) => new(
        message.Id, message.ConversationId, message.SenderUserId, message.DeletedAtUtc is null ? message.Content : null, message.CreatedAtUtc, message.ReadAtUtc,
        message.Type.ToString().ToLowerInvariant(), message.ReplyToMessageId,
        replyTo is null ? null : new MessageReplyPreviewResponse(replyTo.Id, replyTo.SenderUserId, replyTo.DeletedAtUtc is null ? replyTo.Content : null, replyTo.Type.ToString().ToLowerInvariant(), replyTo.DeletedAtUtc is not null),
        message.EditedAtUtc, message.DeletedAtUtc,
        message.DeletedAtUtc is null ? attachments.OrderBy(attachment => attachment.SortOrder).Select(attachment => new MessageAttachmentResponse(attachment.MediaId, attachment.SortOrder)).ToArray() : [],
        message.DeletedAtUtc is null ? reactions.Select(ToReactionResponse).ToArray() : [],
        message.DeletedAtUtc is null
            ? story ?? (message.StoryId is null
                ? null
                : new MessageStoryReferenceResponse(message.StoryId.Value, false, null, null))
            : null);
    private static MessageReactionResponse ToReactionResponse(MessageReaction reaction) => new(reaction.UserId, reaction.Type.ToString().ToLowerInvariant());
    private static bool CanManageConversation(ConversationParticipantRole role) => role is ConversationParticipantRole.Owner or ConversationParticipantRole.Admin;
    private static bool CanRemoveParticipant(ConversationParticipantRole actor, ConversationParticipantRole target, bool self) => target != ConversationParticipantRole.Owner && (actor == ConversationParticipantRole.Owner || (actor == ConversationParticipantRole.Admin && target == ConversationParticipantRole.Member && !self));

    private static ApplicationError? ValidateConversationPagination(string? before, int limit, out ConversationCursor? cursor)
    {
        cursor = null;
        if (limit is < 1 or > MaximumLimit) return new ApplicationError("invalid_pagination", $"Limit must be between 1 and {MaximumLimit}.", ApplicationErrorType.Validation);
        if (string.IsNullOrWhiteSpace(before)) return null;
        return before.Length > MaximumCursorLength || !TryDecodeCursor(before, out cursor) ? new ApplicationError("invalid_conversation_cursor", "The conversation cursor is invalid.", ApplicationErrorType.Validation) : null;
    }
    private static ApplicationError? ValidateMessagePagination(string? before, int limit, out MessageCursor? cursor)
    {
        cursor = null;
        if (limit is < 1 or > MaximumLimit) return new ApplicationError("invalid_pagination", $"Limit must be between 1 and {MaximumLimit}.", ApplicationErrorType.Validation);
        if (string.IsNullOrWhiteSpace(before)) return null;
        return before.Length > MaximumCursorLength || !TryDecodeCursor(before, out cursor) ? new ApplicationError("invalid_message_cursor", "The message history cursor is invalid.", ApplicationErrorType.Validation) : null;
    }
    private static string EncodeConversationCursor(Conversation conversation) => EncodeCursor(new ConversationCursor(conversation.LastMessageAtUtc, conversation.Id));
    private static string EncodeMessageCursor(Message message) => EncodeCursor(new MessageCursor(message.CreatedAtUtc, message.Id));
    private static string EncodeCursor<T>(T cursor) => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(cursor)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static bool TryDecodeCursor(string value, out ConversationCursor? cursor) => TryDecode(value, out cursor);
    private static bool TryDecodeCursor(string value, out MessageCursor? cursor) => TryDecode(value, out cursor);
    private static bool TryDecode<T>(string value, out T? cursor)
    {
        cursor = default;
        try { var base64 = value.Replace('-', '+').Replace('_', '/'); base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='); cursor = JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(Convert.FromBase64String(base64))); return cursor is not null; }
        catch (ArgumentException) { return false; } catch (FormatException) { return false; } catch (JsonException) { return false; }
    }
    private static ApplicationError ToMessagesError(Fookbase.Api.Modules.Media.Common.ApplicationError error) => new(error.Code, error.Message, error.Type switch
    {
        Fookbase.Api.Modules.Media.Common.ApplicationErrorType.NotFound => ApplicationErrorType.NotFound,
        Fookbase.Api.Modules.Media.Common.ApplicationErrorType.Forbidden => ApplicationErrorType.Forbidden,
        Fookbase.Api.Modules.Media.Common.ApplicationErrorType.Conflict => ApplicationErrorType.Conflict,
        _ => ApplicationErrorType.Validation
    });
    private static ApplicationResult<T> Failure<T>(string code, string message, ApplicationErrorType type = ApplicationErrorType.Validation) => ApplicationResult<T>.Failure(new ApplicationError(code, message, type));
    private static ApplicationResult<T> Conflict<T>(string code, string message) => Failure<T>(code, message, ApplicationErrorType.Conflict);
    private static ApplicationResult<T> Forbidden<T>() => Failure<T>("conversation_access_denied", "You do not have access to this conversation.", ApplicationErrorType.Forbidden);
    private sealed record ConversationCursor(DateTimeOffset LastMessageAtUtc, Guid ConversationId);
    private sealed record MessageCursor(DateTimeOffset CreatedAtUtc, Guid MessageId);
}
