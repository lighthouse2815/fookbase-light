namespace Fookbase.Api.Modules.Pages.DTOs.Responses;

public sealed record PageResponse(
    Guid Id,
    string Name,
    string Username,
    string Category,
    string? Bio,
    string Status,
    string? AvatarUrl,
    string? CoverUrl,
    int FollowerCount,
    bool IsFollowing,
    string? ViewerRole,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
