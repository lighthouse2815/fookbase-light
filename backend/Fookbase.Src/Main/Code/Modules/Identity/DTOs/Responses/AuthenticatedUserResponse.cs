namespace Fookbase.Api.Modules.Identity.DTOs.Responses;

public sealed record AuthenticatedUserResponse(
    Guid Id,
    string? Email,
    string? PhoneNumber,
    string Username,
    bool EmailConfirmed,
    bool PhoneNumberConfirmed,
    IReadOnlyList<string> Roles);
