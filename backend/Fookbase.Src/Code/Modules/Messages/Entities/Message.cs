namespace Fookbase.Api.Modules.Messages.Entities;

public enum MessageType
{
    TEXT,
    MEDIA
}

public sealed class Message
{
    private Message()
    {
    }

    private Message(
        Guid id,
        Guid conversationId,
        Guid senderUserId,
        MessageType type,
        string? content,
        Guid? replyToMessageId,
        DateTimeOffset createdAtUtc,
        Guid? storyId)
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

    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public Guid SenderUserId { get; private set; }

    public MessageType Type { get; private set; }

    public string? Content { get; private set; }

    public Guid? ReplyToMessageId { get; private set; }

    public Guid? StoryId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? EditedAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public static Message Create(
        Guid id,
        Guid conversationId,
        Guid senderUserId,
        MessageType type,
        string? content,
        Guid? replyToMessageId,
        DateTimeOffset createdAtUtc,
        Guid? storyId = null) =>
        new(id, conversationId, senderUserId, type, content, replyToMessageId, createdAtUtc, storyId);

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
