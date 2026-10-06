namespace Fookbase.Api.Modules.Events.DTOs.Responses;

public sealed record EventInvitationResponse(
    Guid Id,
    Guid EventId,
    Guid InviterUserId,
    Guid InviteeUserId,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RespondedAtUtc,
    EventResponse? Event = null);
