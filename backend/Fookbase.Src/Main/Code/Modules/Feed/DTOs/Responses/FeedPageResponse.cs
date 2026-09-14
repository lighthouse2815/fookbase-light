using Fookbase.Api.Modules.Posts.DTOs.Responses;
using Fookbase.Api.Modules.Reels.DTOs.Responses;

namespace Fookbase.Api.Modules.Feed.DTOs.Responses;

public sealed record FeedAuthorResponse(
    Guid? UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl);

public sealed record FeedMediaResponse(
    Guid MediaId,
    string MediaType,
    string ContentType);

public sealed record FeedContainerResponse(
    Guid Id,
    string Name,
    string? Username,
    string? Privacy);

public sealed record FeedShareResponse(
    Guid Id,
    Guid OriginalPostId,
    string? Caption,
    DateTimeOffset CreatedAtUtc,
    FeedAuthorResponse Actor,
    PostDisplayIdentityResponse OriginalAuthor,
    PostResponse OriginalPost);

public sealed record FeedItemResponse(
    Guid Id,
    string Content,
    string Privacy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    FeedAuthorResponse Author,
    IReadOnlyList<Guid> MediaIds,
    IReadOnlyList<FeedMediaResponse> Media,
    int CommentCount,
    int ReactionCount,
    IReadOnlyDictionary<string, int> ReactionCounts,
    string? ViewerReaction,
    string ContentType,
    string ContainerType,
    FeedContainerResponse Container,
    PostDisplayIdentityResponse DisplayAuthor,
    ReelVideoResponse? Video,
    bool IsSuggested,
    IReadOnlyList<ContentMentionResponse>? Mentions = null,
    FeedShareResponse? Share = null,
    string? RecommendationReason = null);

public sealed record FeedPageResponse(
    IReadOnlyList<FeedItemResponse> Items,
    string? NextCursor,
    DateTimeOffset AsOfUtc);
