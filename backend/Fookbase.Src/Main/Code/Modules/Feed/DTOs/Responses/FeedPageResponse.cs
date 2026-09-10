namespace Fookbase.Api.Modules.Feed.DTOs.Responses;

public sealed record FeedAuthorResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl);

public sealed record FeedMediaResponse(
    Guid MediaId,
    string MediaType,
    string ContentType);

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
    string? ViewerReaction);

public sealed record FeedPageResponse(
    IReadOnlyList<FeedItemResponse> Items,
    string? NextCursor);
