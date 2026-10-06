using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Messages.Entities;

[Table("ConversationReadCursors")]
[PrimaryKey(nameof(ConversationId), nameof(UserId))]
[Index(nameof(UserId), nameof(ConversationId))]
public sealed class ConversationReadCursor
{
    private ConversationReadCursor() { }

    public ConversationReadCursor(Guid conversationId, Guid userId)
    {
        ConversationId = conversationId;
        UserId = userId;
    }

    public Guid ConversationId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid? LastReadMessageId { get; private set; }

    public DateTimeOffset? LastReadMessageCreatedAtUtc { get; private set; }

    public DateTimeOffset? LastReadAtUtc { get; private set; }

    [ForeignKey(nameof(ConversationId))]
    [InverseProperty(nameof(Conversation.ReadCursors))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Conversation Conversation { get; private set; } = null!;

    [ForeignKey(nameof(UserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User User { get; private set; } = null!;

    [ForeignKey(nameof(LastReadMessageId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Message? LastReadMessage { get; private set; }

    public bool AdvanceTo(Guid messageId, DateTimeOffset messageCreatedAtUtc, DateTimeOffset readAtUtc)
    {
        if (LastReadMessageCreatedAtUtc is not null)
        {
            var chronology = messageCreatedAtUtc.CompareTo(LastReadMessageCreatedAtUtc.Value);
            if (chronology < 0 ||
                chronology == 0 &&
                LastReadMessageId is not null &&
                messageId.CompareTo(LastReadMessageId.Value) <= 0)
            {
                return false;
            }
        }

        LastReadMessageId = messageId;
        LastReadMessageCreatedAtUtc = messageCreatedAtUtc;
        LastReadAtUtc = readAtUtc;
        return true;
    }
}
