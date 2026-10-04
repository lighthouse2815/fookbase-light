namespace Fookbase.Api.Modules.Pages.DTOs.Responses;

public sealed record PageRoleInvitationResponse(Guid Id, Guid PageId, Guid InviterUserId, Guid InviteeUserId,
    string Role, string Status, DateTimeOffset CreatedAtUtc, DateTimeOffset? RespondedAtUtc);
