namespace Fookbase.Api.Modules.Search.DTOs.Responses;

public sealed record SearchReelAuthorResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl);
