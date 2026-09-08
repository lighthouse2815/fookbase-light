namespace Fookbase.Api.Modules.Users.DTOs;

public sealed record UserProfileResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string? CoverUrl,
    DateOnly? DateOfBirth,
    string? CurrentCity,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record UpdateUserProfileRequest(
    string? DisplayName,
    string? Bio,
    DateOnly? DateOfBirth,
    string? CurrentCity);
