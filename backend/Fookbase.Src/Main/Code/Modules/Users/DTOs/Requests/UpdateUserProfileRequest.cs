namespace Fookbase.Api.Modules.Users.DTOs.Requests;

public sealed record UpdateUserProfileRequest(
    string? DisplayName,
    string? Bio,
    DateOnly? DateOfBirth,
    string? CurrentCity);
