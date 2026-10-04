using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Messages.Domain.Enums;
using Fookbase.Api.Modules.Stories.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Messages.Entities;

[Table("Messages")]
[Index(nameof(ConversationId), nameof(CreatedAtUtc), nameof(Id))]
[Index(nameof(ReplyToMessageId))]
[Index(nameof(StoryId))]
public sealed class Message
{
    public const int MaximumContentLength = 5_000;

    private Message()
    {
    }

    public Message(
        Guid id,
        Guid conversationId,
        Guid senderUserId,
        MessageType type,
        string? content,
        Guid? replyToMessageId,
        DateTimeOffset createdAtUtc,
        Guid? storyId = null)
    {
        Id = id;
        ConversationId = conversationId;
        SenderUserId = senderUserId;
        Type = type;
        Content = content;
        ReplyToMessageId = replyToMessageId;
        CreatedAtUtc = createdAtUtc;
        StoryId = storyId;
    }

    [Key]
    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public Guid SenderUserId { get; private set; }

    public MessageType Type { get; private set; }

    [MaxLength(MaximumContentLength)]
    public string? Content { get; private set; }

    public Guid? ReplyToMessageId { get; private set; }

    public Guid? StoryId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? EditedAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    [ForeignKey(nameof(ConversationId))]
    [InverseProperty(nameof(Conversation.Messages))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Conversation Conversation { get; private set; } = null!;

    [ForeignKey(nameof(SenderUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User SenderUser { get; private set; } = null!;

    [ForeignKey(nameof(ReplyToMessageId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Message? ReplyToMessage { get; private set; }

    [ForeignKey(nameof(StoryId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Story? Story { get; private set; }

    [InverseProperty(nameof(MessageAttachment.Message))]
    public ICollection<MessageAttachment> Attachments { get; private set; } = new List<MessageAttachment>();

    [InverseProperty(nameof(MessageReaction.Message))]
    public ICollection<MessageReaction> Reactions { get; private set; } = new List<MessageReaction>();

    [InverseProperty(nameof(MessageNotification.Message))]
    public ICollection<MessageNotification> Notifications { get; private set; } = new List<MessageNotification>();

    public void MarkRead(DateTimeOffset readAtUtc) => ReadAtUtc ??= readAtUtc;

    public void Edit(string content, DateTimeOffset editedAtUtc)
    {
        if (DeletedAtUtc is not null || Type != MessageType.TEXT)
        {
            throw new InvalidOperationException("Only active text messages can be edited.");
        }

        Content = content;
        EditedAtUtc = editedAtUtc;
    }

    public void Delete(DateTimeOffset deletedAtUtc)
    {
        if (DeletedAtUtc is not null)
        {
            return;
        }

        Content = null;
        DeletedAtUtc = deletedAtUtc;
    }
}
