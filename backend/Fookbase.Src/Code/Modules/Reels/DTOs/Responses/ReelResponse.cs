using Fookbase.Api.Modules.Posts.DTOs.Responses;

namespace Fookbase.Api.Modules.Reels.DTOs.Responses;

public sealed record ReelAuthorResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl);

public sealed record ReelVideoResponse(
    Guid MediaId,
    long DurationMs,
    int Width,
    int Height,
    string ContentType,
    string VideoAccessPath,
    string PosterAccessPath);

public sealed record ReelResponse(
    Guid Id,
    ReelAuthorResponse Author,
    string Caption,
    string Privacy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    ReelVideoResponse Video,
    int CommentCount,
    int ReactionCount,
    IReadOnlyDictionary<string, int> ReactionCounts,
    string? ViewerReaction,
    long ViewCount,
    long CompletionCount,
    bool ViewerHasSaved,
    bool ViewerFollowsAuthor,
    IReadOnlyList<ContentMentionResponse>? Mentions = null);
