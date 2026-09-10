namespace Fookbase.Api.Modules.Identity.DTOs.Responses;

public sealed record AuthenticatedUserResponse(
    Guid Id,
    string Email,
    string Username,
    bool EmailConfirmed);
