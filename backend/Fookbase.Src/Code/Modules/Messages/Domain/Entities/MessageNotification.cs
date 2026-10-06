using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Messages.Entities;

[Table("MessageNotifications")]
[Index(nameof(RecipientUserId), nameof(MessageId), IsUnique = true)]
[Index(nameof(RecipientUserId), nameof(ReadAtUtc), nameof(CreatedAtUtc))]
public sealed class MessageNotification
{
    private MessageNotification() { }

    public MessageNotification(
        Guid id,
        Guid recipientUserId,
        Guid conversationId,
        Guid messageId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        RecipientUserId = recipientUserId;
        ConversationId = conversationId;
        MessageId = messageId;
        CreatedAtUtc = createdAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }

    public Guid RecipientUserId { get; private set; }

    public Guid ConversationId { get; private set; }

    public Guid MessageId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    [ForeignKey(nameof(RecipientUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User RecipientUser { get; private set; } = null!;

    [ForeignKey(nameof(ConversationId))]
    [InverseProperty(nameof(Conversation.Notifications))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Conversation Conversation { get; private set; } = null!;

    [ForeignKey(nameof(MessageId))]
    [InverseProperty(nameof(Message.Notifications))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Message Message { get; private set; } = null!;

    public void MarkRead(DateTimeOffset readAtUtc) => ReadAtUtc ??= readAtUtc;
}
