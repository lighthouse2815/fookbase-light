namespace Fookbase.Api.Modules.Feed.DTOs.Responses;

public sealed record FeedAuthorResponse(
    Guid? UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl);
