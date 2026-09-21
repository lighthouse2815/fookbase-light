namespace Fookbase.Api.Modules.Posts.Entities;

public sealed class CommentReaction
{
    private CommentReaction()
    {
    }

    private CommentReaction(
        Guid commentId,
        Guid userId,
        ReactionType type,
        DateTimeOffset createdAtUtc)
    {
        CommentId = commentId;
        UserId = userId;
        Type = type;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid CommentId { get; private set; }

    public Guid UserId { get; private set; }

    public ReactionType Type { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public static CommentReaction Create(
        Guid commentId,
        Guid userId,
        ReactionType type,
        DateTimeOffset createdAtUtc) =>
        new(commentId, userId, type, createdAtUtc);

    public void ChangeTo(ReactionType type, DateTimeOffset updatedAtUtc)
    {
        Type = type;
        UpdatedAtUtc = updatedAtUtc;
    }
}
