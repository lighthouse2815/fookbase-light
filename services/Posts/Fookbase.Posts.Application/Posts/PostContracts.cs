namespace Fookbase.Posts.Application.Posts;

public sealed record PostResponse(
    Guid Id,
    Guid AuthorUserId,
    string Content,
    string Privacy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    int CommentCount,
    IReadOnlyDictionary<string, int> ReactionCounts,
    string? ViewerReaction);

public sealed record CommentResponse(
    Guid Id,
    Guid PostId,
    Guid AuthorUserId,
    Guid? ParentCommentId,
    string Content,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Offset,
    int Limit,
    int Total);
