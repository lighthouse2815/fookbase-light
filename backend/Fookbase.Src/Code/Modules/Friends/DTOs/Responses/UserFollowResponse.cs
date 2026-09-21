namespace Fookbase.Api.Modules.Friends.DTOs.Responses;

public sealed record UserFollowResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    DateTimeOffset FollowedAtUtc);
