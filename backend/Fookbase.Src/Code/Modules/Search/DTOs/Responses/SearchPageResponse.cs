namespace Fookbase.Api.Modules.Search.DTOs.Responses;

public sealed record SearchPageResponse(
    Guid PageId,
    string Name,
    string Username,
    string Category,
    string? Bio,
    string? AvatarUrl,
    int FollowerCount,
    bool ViewerIsFollowing);
