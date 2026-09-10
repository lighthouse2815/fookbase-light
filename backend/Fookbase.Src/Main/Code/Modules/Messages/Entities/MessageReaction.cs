namespace Fookbase.Api.Modules.Messages.Entities;

public enum MessageReactionType
{
    Like,
    Love,
    Haha,
    Wow,
    Sad,
    Angry
}

public sealed class MessageReaction
{
    private MessageReaction()
    {
    }

    private MessageReaction(Guid messageId, Guid userId, MessageReactionType type, DateTimeOffset createdAtUtc)
    {
        MessageId = messageId;
        UserId = userId;
        Type = type;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid MessageId { get; private set; }
    public Guid UserId { get; private set; }
    public MessageReactionType Type { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public static MessageReaction Create(Guid messageId, Guid userId, MessageReactionType type, DateTimeOffset createdAtUtc) =>
        new(messageId, userId, type, createdAtUtc);

    public void ChangeTo(MessageReactionType type, DateTimeOffset updatedAtUtc)
    {
        Type = type;
        UpdatedAtUtc = updatedAtUtc;
    }
}
