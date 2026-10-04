using Fookbase.Api.Modules.Messages.Domain.Enums;

namespace Fookbase.Api.Modules.Messages.Entities;

public sealed class MessageReaction
{
    private MessageReaction()
    {
    }

    public MessageReaction(Guid messageId, Guid userId, MessageReactionType type, DateTimeOffset createdAtUtc)
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

    public void ChangeTo(MessageReactionType type, DateTimeOffset updatedAtUtc)
    {
        Type = type;
        UpdatedAtUtc = updatedAtUtc;
    }
}
