namespace Fookbase.Api.Modules.Groups.DTOs.Responses;

public sealed record GroupInviteResponse(
    Guid Id,
    Guid GroupId,
    Guid InviterUserId,
    Guid InviteeUserId,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RespondedAtUtc);
