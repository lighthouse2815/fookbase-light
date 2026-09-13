namespace Fookbase.Api.Modules.Users.DTOs.Responses;

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
    DateTimeOffset UpdatedAt,
    int FollowerCount,
    int FollowingCount,
    bool? IsFollowing,
    bool? IsFollowedBy,
    string? FriendshipState);
