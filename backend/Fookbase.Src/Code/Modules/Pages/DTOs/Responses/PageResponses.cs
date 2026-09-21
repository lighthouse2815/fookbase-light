using Fookbase.Api.Modules.Posts.DTOs.Responses;

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

public sealed record PageMemberResponse(Guid UserId, string Username, string DisplayName, string Role,
    DateTimeOffset JoinedAtUtc);
public sealed record PageRoleInvitationResponse(Guid Id, Guid PageId, Guid InviterUserId, Guid InviteeUserId,
    string Role, string Status, DateTimeOffset CreatedAtUtc, DateTimeOffset? RespondedAtUtc);
public sealed record PageCursorPageResponse<T>(IReadOnlyList<T> Items, string? NextCursor);
public sealed record PageTimelineResponse(IReadOnlyList<PostResponse> Items, string? NextCursor);
