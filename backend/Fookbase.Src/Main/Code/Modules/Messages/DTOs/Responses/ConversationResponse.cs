namespace Fookbase.Api.Modules.Messages.DTOs.Responses;

public sealed record ConversationResponse(
    Guid Id,
    Guid ParticipantUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastMessageAtUtc,
    MessageResponse? LastMessage,
    int UnreadCount);
