using Fookbase.Api.Modules.Posts.DTOs.Responses;

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

public sealed record SearchGroupResponse(
    Guid GroupId,
    string Name,
    string? Description,
    string Privacy,
    string? CoverUrl,
    int MemberCount,
    string? ViewerMembershipState);

public sealed record SearchPageResponse(
    Guid PageId,
    string Name,
    string Username,
    string Category,
    string? Bio,
    string? AvatarUrl,
    int FollowerCount,
    bool ViewerIsFollowing);

public sealed record SearchPostResponse(
    Guid PostId,
    Guid? AuthorUserId,
    PostDisplayIdentityResponse? DisplayAuthor,
    string Snippet,
    IReadOnlyList<Guid> MediaIds,
    int CommentCount,
    IReadOnlyDictionary<string, int> ReactionCounts,
    string ContainerType,
    Guid ContainerId,
    DateTimeOffset CreatedAtUtc);

public sealed record SearchReelAuthorResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl);

public sealed record SearchReelMediaResponse(
    Guid MediaId,
    long DurationMs,
    int Width,
    int Height,
    string VideoAccessPath,
    string PosterAccessPath);

public sealed record SearchReelResponse(
    Guid ReelId,
    SearchReelAuthorResponse Author,
    string Snippet,
    SearchReelMediaResponse Media,
    int CommentCount,
    int ReactionCount,
    long ViewCount,
    DateTimeOffset CreatedAtUtc);

public sealed record SearchHashtagResponse(
    string Tag,
    string DisplayName);

public sealed record GlobalSearchResponse(
    IReadOnlyList<SearchPersonResponse> People,
    IReadOnlyList<SearchGroupResponse> Groups,
    IReadOnlyList<SearchPageResponse> Pages,
    IReadOnlyList<SearchPostResponse> Posts,
    IReadOnlyList<SearchReelResponse> Reels,
    string? NextCursor = null,
    IReadOnlyList<SearchHashtagResponse>? Hashtags = null);

public sealed record SearchSuggestionsResponse(
    IReadOnlyList<SearchPersonResponse> People,
    IReadOnlyList<SearchGroupResponse> Groups,
    IReadOnlyList<SearchPageResponse> Pages);
