namespace Fookbase.Api.Modules.Stories.DTOs.Responses;

public sealed record StoryAuthorResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl);
