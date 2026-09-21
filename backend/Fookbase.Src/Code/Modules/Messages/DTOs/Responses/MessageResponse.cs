namespace Fookbase.Api.Modules.Messages.DTOs.Responses;

public sealed record MessageStoryReferenceResponse(
    Guid StoryId,
    bool IsAvailable,
    string? Caption,
    string? MediaType);

public sealed record MessageResponse(
    Guid Id,
    Guid ConversationId,
    Guid SenderUserId,
    string? Content,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc,
    string Type = "text",
    Guid? ReplyToMessageId = null,
    MessageReplyPreviewResponse? ReplyTo = null,
    DateTimeOffset? EditedAtUtc = null,
    DateTimeOffset? DeletedAtUtc = null,
    IReadOnlyList<MessageAttachmentResponse>? Attachments = null,
    IReadOnlyList<MessageReactionResponse>? Reactions = null,
    MessageStoryReferenceResponse? Story = null);
