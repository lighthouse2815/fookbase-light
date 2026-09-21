namespace Fookbase.Api.Modules.Messages.DTOs.Responses;

public sealed record ConversationParticipantResponse(
    Guid UserId,
    string Role,
    DateTimeOffset JoinedAtUtc,
    DateTimeOffset? LeftAtUtc,
    Guid? LastReadMessageId,
    DateTimeOffset? LastReadAtUtc,
    Guid? LastDeliveredMessageId,
    string? Nickname);
