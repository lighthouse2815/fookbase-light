namespace Fookbase.Api.Modules.Events.DTOs.Responses;

public sealed record EventParticipantResponse(Guid UserId, string Username, string DisplayName, string? AvatarUrl,
    string Status, DateTimeOffset RespondedAtUtc);
