namespace Fookbase.Api.Modules.Search.DTOs.Responses;

public sealed record SearchPersonResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    string? Bio,
    int FollowerCount,
    int FollowingCount,
    bool? IsFollowing,
    bool? IsFollowedBy,
    string? FriendshipState);
