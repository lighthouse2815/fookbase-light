using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Media.DTOs.Responses;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Messages.Common;
using Fookbase.Api.Modules.Messages.DTOs.Requests;
using Fookbase.Api.Modules.Messages.DTOs.Responses;
using Fookbase.Api.Modules.Messages.Domain.Enums;
using Fookbase.Api.Modules.Messages.Entities;
using Fookbase.Api.Modules.Messages.Hubs;
using Fookbase.Api.Modules.Notifications.Services;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Messages.Services;

public sealed class MessagesService(
    FookbaseDbContext dbContext,
    TimeProvider timeProvider,
    IHubContext<MessagesHub> hubContext,
    FriendsService friendsService,
    MediaService mediaService,
    PushNotificationService pushNotifications)
{
    public const int MaximumLimit = 100;
    public const int MaximumContentLength = Message.MaximumContentLength;
    public const int MaximumGroupSize = 50;

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
            conversation = new Conversation(Guid.NewGuid(), actorUserId, participantUserId, now);
            dbContext.Conversations.Add(conversation);
            dbContext.ConversationParticipants.AddRange(
                new ConversationParticipant(conversation.Id, conversation.UserId1!.Value, ConversationParticipantRole.MEMBER, now),
                new ConversationParticipant(conversation.Id, conversation.UserId2!.Value, ConversationParticipantRole.MEMBER, now));
            // Kept as a compatibility projection for existing direct chat clients/imports.
            dbContext.ConversationReadCursors.AddRange(
                new ConversationReadCursor(conversation.Id, conversation.UserId1.Value),
                new ConversationReadCursor(conversation.Id, conversation.UserId2.Value));
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
        var participantIds = request.ParticipantUserIds.ToArray();
        if (participantIds.Contains(actorUserId))
            return Failure<ConversationResponse>("invalid_group_participants", "The creator is already a group participant.");
        if (await dbContext.Users.AsNoTracking().CountAsync(user => participantIds.Contains(user.Id), cancellationToken) != participantIds.Length)
            return Failure<ConversationResponse>("group_participant_not_found", "Every group participant must be an existing user.", ApplicationErrorType.NOT_FOUND);
        if (request.PhotoMediaId is not null)
        {
            var media = await mediaService.ValidateGroupCoverImageAsync(actorUserId, request.PhotoMediaId.Value, cancellationToken);
            if (!media.Succeeded) return ApplicationResult<ConversationResponse>.Failure(media.Error!);
        }

        var now = timeProvider.GetUtcNow();
        Conversation conversation;
        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            conversation = new Conversation(Guid.NewGuid(), request.Title, now);
            conversation.UpdateGroup(request.Title, request.PhotoMediaId);
            dbContext.Conversations.Add(conversation);
            dbContext.ConversationParticipants.Add(new ConversationParticipant(conversation.Id, actorUserId, ConversationParticipantRole.OWNER, now));
            dbContext.ConversationParticipants.AddRange(participantIds.Select(id => new ConversationParticipant(conversation.Id, id, ConversationParticipantRole.MEMBER, now)));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        var response = await BuildConversationResponseAsync(conversation, actorUserId, cancellationToken);
        await hubContext.Clients.Users(participantIds.Append(actorUserId).Select(id => id.ToString())).SendAsync("ConversationCreated", response, cancellationToken);
        return ApplicationResult<ConversationResponse>.Success(response);
    }

    public async Task<ApplicationResult<PagedResponse<ConversationResponse>>> GetConversationsAsync(Guid actorUserId, string? before, int limit, bool includeArchived, CancellationToken cancellationToken = default)
    {
        var cursor = string.IsNullOrWhiteSpace(before) ? null : MessageCursorCodec.Decode<ConversationCursor>(before);

        var snapshot = await friendsService.GetAccessSnapshotAsync(actorUserId, cancellationToken);
        var permittedDirectUsers = snapshot.FriendUserIds.Except(snapshot.BlockedUserIds).ToArray();
        var query = from participant in dbContext.ConversationParticipants.AsNoTracking()
                    join conversation in dbContext.Conversations.AsNoTracking() on participant.ConversationId equals conversation.Id
                    where participant.UserId == actorUserId && participant.LeftAtUtc == null &&
                          (includeArchived || participant.ArchivedAtUtc == null) &&
                          (conversation.Type != ConversationType.DIRECT ||
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
            if (conversation.Type != ConversationType.GROUP || !CanManageConversation(participant.Role)) return Forbidden<ConversationResponse>();
            if (request.PhotoMediaId is not null)
            {
                var media = await mediaService.ValidateGroupCoverImageAsync(actorUserId, request.PhotoMediaId.Value, cancellationToken);
                if (!media.Succeeded) return ApplicationResult<ConversationResponse>.Failure(media.Error!);
            }
            conversation.UpdateGroup(request.Title ?? conversation.Title!, request.RemovePhoto ? null : request.PhotoMediaId ?? conversation.PhotoMediaId);
        }
        if (request.Archived is not null) participant.SetArchived(request.Archived.Value, timeProvider.GetUtcNow());
        if (request.MutedUntilUtc is not null) participant.SetMutedUntil(request.MutedUntilUtc);
        if (request.Nickname is not null) participant.SetNickname(request.Nickname);

        await dbContext.SaveChangesAsync(cancellationToken);
        var response = await BuildConversationResponseAsync(conversation, actorUserId, cancellationToken);
        await NotifyActiveParticipantsAsync(conversation, actorUserId, "ConversationUpdated", response, cancellationToken);
        return ApplicationResult<ConversationResponse>.Success(response);
    }

    public async Task<ApplicationResult<MessageHistoryResponse>> GetMessagesAsync(Guid actorUserId, Guid conversationId, string? before, int limit, CancellationToken cancellationToken = default)
    {
        var cursor = string.IsNullOrWhiteSpace(before) ? null : MessageCursorCodec.Decode<MessageCursor>(before);
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
        if (lastRead is null) return Failure<bool>("message_not_found", "The message was not found in this conversation.", ApplicationErrorType.NOT_FOUND);
        var now = timeProvider.GetUtcNow();
        if (!participant.AdvanceReadCursor(lastRead.Id, lastRead.CreatedAtUtc, now)) return ApplicationResult<bool>.Success(true);
        if (conversation.Type == ConversationType.DIRECT)
        {
            var legacyCursor = await dbContext.ConversationReadCursors.SingleOrDefaultAsync(cursor => cursor.ConversationId == conversationId && cursor.UserId == actorUserId, cancellationToken);
            if (legacyCursor is null) { legacyCursor = new ConversationReadCursor(conversationId, actorUserId); dbContext.ConversationReadCursors.Add(legacyCursor); }
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
            if (!media.Succeeded) return ApplicationResult<MessageResponse>.Failure(media.Error!);
        }
        var now = timeProvider.GetUtcNow();
        var message = new Message(Guid.NewGuid(), conversationId, actorUserId, attachmentIds.Length == 0 ? MessageType.TEXT : MessageType.MEDIA,
            string.IsNullOrWhiteSpace(content) ? null : content, request.ReplyToMessageId, now, storyId);
        conversation.RecordMessage(now);
        dbContext.Messages.Add(message);
        dbContext.MessageAttachments.AddRange(attachmentIds.Select((mediaId, index) => new MessageAttachment(message.Id, mediaId, index)));
        var recipients = await GetVisibleRecipientUserIdsAsync(conversation, actorUserId, cancellationToken);
        var recipientParticipants = await dbContext.ConversationParticipants.Where(participant => participant.ConversationId == conversationId && participant.LeftAtUtc == null && recipients.Contains(participant.UserId)).ToListAsync(cancellationToken);
        foreach (var recipient in recipientParticipants)
        {
            recipient.MarkDelivered(message.Id);
            dbContext.MessageNotifications.Add(new MessageNotification(Guid.NewGuid(), recipient.UserId, conversationId, message.Id, now));
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
        await pushNotifications.SendMessageAsync(recipients, conversationId, cancellationToken);
        return ApplicationResult<MessageResponse>.Success(response);
    }

    public async Task<ApplicationResult<MessageResponse>> EditMessageAsync(Guid actorUserId, Guid messageId, EditMessageRequest request, CancellationToken cancellationToken = default)
    {
        var content = request.Content.Trim();
        var message = await dbContext.Messages.SingleOrDefaultAsync(item => item.Id == messageId, cancellationToken);
        if (message is null) return Failure<MessageResponse>("message_not_found", "The message was not found.", ApplicationErrorType.NOT_FOUND);
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
        if (message is null) return Failure<bool>("message_not_found", "The message was not found.", ApplicationErrorType.NOT_FOUND);
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
        var type = Enum.Parse<MessageReactionType>(request.Type, true);
        var message = await dbContext.Messages.SingleOrDefaultAsync(item => item.Id == messageId, cancellationToken);
        if (message is null || message.DeletedAtUtc is not null) return Failure<MessageReactionResponse>("message_not_found", "The message was not found.", ApplicationErrorType.NOT_FOUND);
        var access = await GetActiveConversationAccessAsync(actorUserId, message.ConversationId, cancellationToken);
        if (!access.Succeeded) return ApplicationResult<MessageReactionResponse>.Failure(access.Error!);
        var relationshipError = await ValidateConversationRelationshipAsync(access.Value!.Conversation, actorUserId, cancellationToken);
        if (relationshipError is not null) return ApplicationResult<MessageReactionResponse>.Failure(relationshipError);
        var visibilityError = await ValidateMessageVisibilityAsync(actorUserId, message, cancellationToken);
        if (visibilityError is not null) return ApplicationResult<MessageReactionResponse>.Failure(visibilityError);
        var reaction = await dbContext.MessageReactions.SingleOrDefaultAsync(item => item.MessageId == messageId && item.UserId == actorUserId, cancellationToken);
        if (reaction is null) { reaction = new MessageReaction(messageId, actorUserId, type, timeProvider.GetUtcNow()); dbContext.MessageReactions.Add(reaction); }
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
        if (message is null || message.DeletedAtUtc is not null) return Failure<bool>("message_not_found", "The message was not found.", ApplicationErrorType.NOT_FOUND);
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
        var userIds = request.UserIds.ToArray();
        var access = await GetActiveConversationAccessAsync(actorUserId, conversationId, cancellationToken);
        if (!access.Succeeded) return ApplicationResult<ConversationResponse>.Failure(access.Error!);
        var (conversation, actor) = access.Value!;
        if (conversation.Type != ConversationType.GROUP || !CanManageConversation(actor.Role)) return Forbidden<ConversationResponse>();
        var existing = await dbContext.ConversationParticipants.Where(participant => participant.ConversationId == conversationId).ToListAsync(cancellationToken);
        var newIds = userIds.Except(existing.Where(participant => participant.LeftAtUtc is null).Select(participant => participant.UserId)).ToArray();
        if (existing.Count(participant => participant.LeftAtUtc is null) + newIds.Length > MaximumGroupSize) return Failure<ConversationResponse>("group_size_limit", $"A group can contain at most {MaximumGroupSize} participants.");
        if (await dbContext.Users.AsNoTracking().CountAsync(user => newIds.Contains(user.Id), cancellationToken) != newIds.Length) return Failure<ConversationResponse>("group_participant_not_found", "Every group participant must be an existing user.", ApplicationErrorType.NOT_FOUND);
        var now = timeProvider.GetUtcNow();
        var latest = await dbContext.Messages.AsNoTracking().Where(message => message.ConversationId == conversationId).OrderByDescending(message => message.CreatedAtUtc).ThenByDescending(message => message.Id).FirstOrDefaultAsync(cancellationToken);
        foreach (var userId in newIds)
        {
            var target = existing.SingleOrDefault(participant => participant.UserId == userId);
            if (target is null) { target = new ConversationParticipant(conversationId, userId, ConversationParticipantRole.MEMBER, now); dbContext.ConversationParticipants.Add(target); }
            else target.Rejoin(ConversationParticipantRole.MEMBER, now);
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
        if (conversation.Type != ConversationType.GROUP) return Failure<bool>("invalid_conversation_type", "Only group conversations have removable participants.");
        var target = await dbContext.ConversationParticipants.SingleOrDefaultAsync(participant => participant.ConversationId == conversationId && participant.UserId == userId && participant.LeftAtUtc == null, cancellationToken);
        if (target is null) return Failure<bool>("participant_not_found", "The participant is not active in this conversation.", ApplicationErrorType.NOT_FOUND);
        if (!CanRemoveParticipant(actor.Role, target.Role, actorUserId == userId)) return Forbidden<bool>();
        target.Leave(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        await hubContext.Clients.User(userId.ToString()).SendAsync("ParticipantRemoved", new { ConversationId = conversationId, UserId = userId }, cancellationToken);
        await NotifyActiveParticipantsAsync(conversation, actorUserId, "ParticipantRemoved", new { ConversationId = conversationId, UserId = userId }, cancellationToken);
        return ApplicationResult<bool>.Success(true);
    }

    public async Task<ApplicationResult<bool>> ChangeParticipantRoleAsync(Guid actorUserId, Guid conversationId, Guid userId, ChangeConversationParticipantRoleRequest request, CancellationToken cancellationToken = default)
    {
        var role = Enum.Parse<ConversationParticipantRole>(request.Role, true);
        var access = await GetActiveConversationAccessAsync(actorUserId, conversationId, cancellationToken);
        if (!access.Succeeded) return ApplicationResult<bool>.Failure(access.Error!);
        var (conversation, actor) = access.Value!;
        if (conversation.Type != ConversationType.GROUP || actor.Role != ConversationParticipantRole.OWNER) return Forbidden<bool>();
        var target = await dbContext.ConversationParticipants.SingleOrDefaultAsync(participant => participant.ConversationId == conversationId && participant.UserId == userId && participant.LeftAtUtc == null, cancellationToken);
        if (target is null) return Failure<bool>("participant_not_found", "The participant is not active in this conversation.", ApplicationErrorType.NOT_FOUND);
        if (target.Role == ConversationParticipantRole.OWNER) return Conflict<bool>("owner_role_protected", "Transfer ownership before changing the owner's role.");
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
        if (conversation.Type != ConversationType.GROUP) return Failure<bool>("invalid_conversation_type", "Direct conversation participants cannot leave the conversation.");
        if (participant.Role == ConversationParticipantRole.OWNER) return Conflict<bool>("owner_must_transfer", "Transfer ownership before leaving this group conversation.");
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
        if (conversation.Type != ConversationType.GROUP || owner.Role != ConversationParticipantRole.OWNER) return Forbidden<bool>();
        var target = await dbContext.ConversationParticipants.SingleOrDefaultAsync(participant => participant.ConversationId == conversationId && participant.UserId == request.UserId && participant.LeftAtUtc == null, cancellationToken);
        if (target is null || target.UserId == actorUserId) return Failure<bool>("participant_not_found", "Ownership must be transferred to another active participant.");
        owner.ChangeRole(ConversationParticipantRole.ADMIN);
        target.ChangeRole(ConversationParticipantRole.OWNER);
        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyActiveParticipantsAsync(conversation, actorUserId, "ConversationUpdated", new { ConversationId = conversationId, OwnerUserId = target.UserId }, cancellationToken);
        return ApplicationResult<bool>.Success(true);
    }

    public async Task<ApplicationResult<IReadOnlyList<MessageResponse>>> SearchMessagesAsync(Guid actorUserId, Guid conversationId, string queryText, CancellationToken cancellationToken = default)
    {
        var query = queryText.Trim();
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
        if (!authorized) return Failure<MediaReadUrlResponse>("media_access_denied", "You do not have access to this conversation media.", ApplicationErrorType.FORBIDDEN);
        return await mediaService.CreateReadUrlAsync(mediaId, cancellationToken);
    }

    public async Task<ApplicationResult<PagedResponse<IncomingMessageResponse>>> GetUnreadNotificationsAsync(Guid actorUserId, int offset, int limit, CancellationToken cancellationToken = default)
    {
        var accessSnapshot = await friendsService.GetAccessSnapshotAsync(actorUserId, cancellationToken);
        var blocked = accessSnapshot.BlockedUserIds;
        var permittedDirectUsers = accessSnapshot.FriendUserIds.Except(blocked).ToArray();
        var query = from notification in dbContext.MessageNotifications.AsNoTracking()
                    join message in dbContext.Messages.AsNoTracking() on notification.MessageId equals message.Id
                    join participant in dbContext.ConversationParticipants.AsNoTracking() on new { notification.ConversationId, UserId = actorUserId } equals new { participant.ConversationId, participant.UserId }
                    join conversation in dbContext.Conversations.AsNoTracking() on notification.ConversationId equals conversation.Id
                    where notification.RecipientUserId == actorUserId && notification.ReadAtUtc == null && participant.LeftAtUtc == null && !blocked.Contains(message.SenderUserId) && message.DeletedAtUtc == null &&
                          (conversation.Type != ConversationType.DIRECT ||
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
        if (conversation is null) return Failure<(Conversation, ConversationParticipant)>("conversation_not_found", "Conversation was not found.", ApplicationErrorType.NOT_FOUND);
        var participant = await dbContext.ConversationParticipants.SingleOrDefaultAsync(item => item.ConversationId == conversationId && item.UserId == actorUserId && item.LeftAtUtc == null, cancellationToken);
        return participant is null ? Forbidden<(Conversation, ConversationParticipant)>() : ApplicationResult<(Conversation, ConversationParticipant)>.Success((conversation, participant));
    }

    private async Task<Conversation?> FindDirectConversationAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken)
    {
        var (userId1, userId2) = firstUserId.CompareTo(secondUserId) < 0 ? (firstUserId, secondUserId) : (secondUserId, firstUserId);
        return await dbContext.Conversations.SingleOrDefaultAsync(conversation => conversation.Type == ConversationType.DIRECT && conversation.UserId1 == userId1 && conversation.UserId2 == userId2, cancellationToken);
    }

    private async Task<ApplicationError?> ValidateConversationRelationshipAsync(Conversation conversation, Guid actorUserId, CancellationToken cancellationToken)
    {
        if (conversation.Type != ConversationType.DIRECT) return null;
        var otherUserId = await dbContext.ConversationParticipants.AsNoTracking()
            .Where(participant => participant.ConversationId == conversation.Id && participant.UserId != actorUserId && participant.LeftAtUtc == null)
            .Select(participant => (Guid?)participant.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        return otherUserId is null
            ? new ApplicationError("conversation_access_denied", "You do not have access to this conversation.", ApplicationErrorType.FORBIDDEN)
            : await ValidateDirectRelationshipAsync(actorUserId, otherUserId.Value, cancellationToken);
    }

    private async Task<ApplicationError?> ValidateDirectRelationshipAsync(Guid actorUserId, Guid otherUserId, CancellationToken cancellationToken)
    {
        var snapshot = await friendsService.GetAccessSnapshotAsync(actorUserId, cancellationToken);
        if (snapshot.BlockedUserIds.Contains(otherUserId)) return new ApplicationError("messaging_blocked", "Messaging is unavailable because one of you has blocked the other.", ApplicationErrorType.FORBIDDEN);
        return snapshot.FriendUserIds.Contains(otherUserId) ? null : new ApplicationError("messaging_requires_friendship", "You can only message friends.", ApplicationErrorType.FORBIDDEN);
    }

    private async Task<ApplicationError?> ValidateMessageVisibilityAsync(Guid actorUserId, Message message, CancellationToken cancellationToken)
    {
        if (message.SenderUserId == actorUserId) return null;
        var blockedUserIds = (await friendsService.GetAccessSnapshotAsync(actorUserId, cancellationToken)).BlockedUserIds;
        return blockedUserIds.Contains(message.SenderUserId)
            ? new ApplicationError("message_access_denied", "You do not have access to this message.", ApplicationErrorType.FORBIDDEN)
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
                .Where(asset => mediaIds.Contains(asset.Id) && asset.Status == MediaStatus.READY &&
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
                 (story.Privacy == PostPrivacy.PUBLIC ||
                  (story.Privacy == PostPrivacy.FRIENDS &&
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
        conversation.Id, conversation.Type == ConversationType.DIRECT ? participants.SingleOrDefault(participant => participant.UserId != actorUserId)?.UserId : null, conversation.CreatedAtUtc, conversation.LastMessageAtUtc,
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
    private static bool CanManageConversation(ConversationParticipantRole role) => role is ConversationParticipantRole.OWNER or ConversationParticipantRole.ADMIN;
    private static bool CanRemoveParticipant(ConversationParticipantRole actor, ConversationParticipantRole target, bool self) => target != ConversationParticipantRole.OWNER && (actor == ConversationParticipantRole.OWNER || (actor == ConversationParticipantRole.ADMIN && target == ConversationParticipantRole.MEMBER && !self));

    private static string EncodeConversationCursor(Conversation conversation) =>
        MessageCursorCodec.Encode(new ConversationCursor(conversation.LastMessageAtUtc, conversation.Id));
    private static string EncodeMessageCursor(Message message) =>
        MessageCursorCodec.Encode(new MessageCursor(message.CreatedAtUtc, message.Id));
    private static ApplicationResult<T> Failure<T>(string code, string message, ApplicationErrorType type = ApplicationErrorType.VALIDATION) => ApplicationResult<T>.Failure(new ApplicationError(code, message, type));
    private static ApplicationResult<T> Conflict<T>(string code, string message) => Failure<T>(code, message, ApplicationErrorType.CONFLICT);
    private static ApplicationResult<T> Forbidden<T>() => Failure<T>("conversation_access_denied", "You do not have access to this conversation.", ApplicationErrorType.FORBIDDEN);
}
