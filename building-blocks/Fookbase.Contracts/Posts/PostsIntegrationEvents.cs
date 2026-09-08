namespace Fookbase.Contracts.Posts;

public sealed record PostCreatedIntegrationEvent(
    Guid EventId,
    Guid PostId,
    Guid AuthorUserId,
    string Privacy,
    string Content,
    DateTimeOffset CreatedAtUtc)
{
    public const string EventType = "posts.post.created.v1";
}

public sealed record PostUpdatedIntegrationEvent(
    Guid EventId,
    Guid PostId,
    Guid AuthorUserId,
    string Privacy,
    string Content,
    DateTimeOffset UpdatedAtUtc)
{
    public const string EventType = "posts.post.updated.v1";
}

public sealed record PostDeletedIntegrationEvent(
    Guid EventId,
    Guid PostId,
    Guid AuthorUserId,
    DateTimeOffset OccurredAtUtc)
{
    public const string EventType = "posts.post.deleted.v1";
}

public sealed record CommentCreatedIntegrationEvent(
    Guid EventId,
    Guid CommentId,
    Guid PostId,
    Guid PostAuthorUserId,
    Guid CommentAuthorUserId,
    Guid? ParentCommentId,
    DateTimeOffset OccurredAtUtc)
{
    public const string EventType = "posts.comment.created.v1";
}

public sealed record PostReactionChangedIntegrationEvent(
    Guid EventId,
    Guid PostId,
    Guid PostAuthorUserId,
    Guid ReactorUserId,
    string? ReactionType,
    DateTimeOffset OccurredAtUtc)
{
    public const string EventType = "posts.reaction.changed.v1";
}

public sealed record PostMediaAttachedIntegrationEvent(
    Guid EventId,
    Guid PostId,
    Guid MediaId,
    Guid AuthorUserId,
    int SortOrder,
    DateTimeOffset OccurredAtUtc)
{
    public const string EventType = "posts.media.attached.v1";
}

public sealed record PostMediaDetachedIntegrationEvent(
    Guid EventId,
    Guid PostId,
    Guid MediaId,
    Guid AuthorUserId,
    DateTimeOffset OccurredAtUtc)
{
    public const string EventType = "posts.media.detached.v1";
}
