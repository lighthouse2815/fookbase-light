namespace Fookbase.Api.Modules.Messages.DTOs.Responses;

public sealed record ConversationResponse(
    Guid Id,
    Guid? ParticipantUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastMessageAtUtc,
    MessageResponse? LastMessage,
    int UnreadCount,
    string Type = "direct",
    string? Title = null,
    Guid? PhotoMediaId = null,
    IReadOnlyList<ConversationParticipantResponse>? Participants = null,
    bool IsMuted = false,
    bool IsArchived = false);
