namespace Fookbase.Api.Modules.Pages.DTOs.Requests;

public sealed record CreatePageRoleInvitationRequest(Guid UserId, string Role);
