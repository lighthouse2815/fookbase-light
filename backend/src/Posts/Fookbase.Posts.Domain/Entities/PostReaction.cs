namespace Fookbase.Posts.Domain.Entities;

public enum ReactionType
{
    Like,
    Love,
    Haha,
    Wow,
    Sad,
    Angry
}

public sealed class PostReaction
{
    private PostReaction()
    {
    }

    private PostReaction(
        Guid postId,
        Guid userId,
        ReactionType type,
        DateTimeOffset createdAtUtc)
    {
        PostId = postId;
        UserId = userId;
        Type = type;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid PostId { get; private set; }

    public Guid UserId { get; private set; }

    public ReactionType Type { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public static PostReaction Create(
        Guid postId,
        Guid userId,
        ReactionType type,
        DateTimeOffset createdAtUtc) =>
        new(postId, userId, type, createdAtUtc);

    public void ChangeTo(ReactionType type, DateTimeOffset updatedAtUtc)
    {
        Type = type;
        UpdatedAtUtc = updatedAtUtc;
    }
}
