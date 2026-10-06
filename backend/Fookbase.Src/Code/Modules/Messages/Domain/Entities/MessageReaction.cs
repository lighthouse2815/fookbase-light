using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Messages.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Messages.Entities;

[PrimaryKey(nameof(MessageId), nameof(UserId))]
[Index(nameof(MessageId), nameof(Type))]
public sealed class MessageReaction
{
    private MessageReaction() { }

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

    [InverseProperty(nameof(Message.Reactions))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Message Message { get; private set; } = null!;

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User User { get; private set; } = null!;

    public void ChangeTo(MessageReactionType type, DateTimeOffset updatedAtUtc)
    {
        Type = type;
        UpdatedAtUtc = updatedAtUtc;
    }
}
