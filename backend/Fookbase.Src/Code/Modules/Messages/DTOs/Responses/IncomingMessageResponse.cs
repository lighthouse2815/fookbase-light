namespace Fookbase.Api.Modules.Messages.DTOs.Responses;

public sealed record IncomingMessageResponse(
    ConversationResponse Conversation,
    MessageResponse Message);
