namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record CreateGroupConversationRequest(
    string? Title,
    IReadOnlyList<Guid>? ParticipantUserIds,
    Guid? PhotoMediaId = null);
