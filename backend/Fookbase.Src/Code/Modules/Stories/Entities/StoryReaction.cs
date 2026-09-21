namespace Fookbase.Api.Modules.Stories.Entities;

public enum StoryReactionType
{
    Like,
    Love,
    Haha,
    Wow,
    Sad,
    Angry
}

public sealed class StoryReaction
{
    private StoryReaction() { }

    private StoryReaction(Guid storyId, Guid userId, StoryReactionType type, DateTimeOffset createdAtUtc)
    {
        StoryId = storyId;
        UserId = userId;
        Type = type;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid StoryId { get; private set; }
    public Guid UserId { get; private set; }
    public StoryReactionType Type { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static StoryReaction Create(Guid storyId, Guid userId, StoryReactionType type, DateTimeOffset createdAtUtc) =>
        new(storyId, userId, type, createdAtUtc);

    public void Change(StoryReactionType type, DateTimeOffset createdAtUtc)
    {
        Type = type;
        CreatedAtUtc = createdAtUtc;
    }
}
