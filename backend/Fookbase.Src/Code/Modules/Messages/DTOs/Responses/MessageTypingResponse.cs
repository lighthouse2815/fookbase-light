namespace Fookbase.Api.Modules.Messages.DTOs.Responses;

public sealed record MessageTypingResponse(Guid ConversationId, Guid SenderUserId);
