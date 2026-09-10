namespace Fookbase.Api.Modules.Messages.DTOs.Responses;

public sealed record MessageResponse(
    Guid Id,
    Guid ConversationId,
    Guid SenderUserId,
    string Content,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc);
