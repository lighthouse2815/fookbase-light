namespace Fookbase.Api.Modules.Users.DTOs.Requests;

using Fookbase.Api.Modules.Users.Entities;

public sealed record UpdateUserProfileRequest(
    string? DisplayName,
    string? Bio,
    DateOnly? DateOfBirth,
    string? CurrentCity,
    Guid? AvatarMediaId = null,
    Guid? CoverMediaId = null,
    BirthdayVisibility? BirthdayVisibility = null,
    string? Hometown = null,
    string? Workplace = null,
    string? Education = null,
    string? Website = null);
