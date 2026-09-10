namespace Fookbase.Api.Modules.Messages.Entities;

public sealed class Message
{
    private Message()
    {
    }

    private Message(
        Guid id,
        Guid conversationId,
        Guid senderUserId,
        string content,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        ConversationId = conversationId;
        SenderUserId = senderUserId;
        Content = content;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public Guid SenderUserId { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public static Message Create(
        Guid id,
        Guid conversationId,
        Guid senderUserId,
        string content,
        DateTimeOffset createdAtUtc) =>
        new(id, conversationId, senderUserId, content, createdAtUtc);

    public void MarkRead(DateTimeOffset readAtUtc) => ReadAtUtc ??= readAtUtc;
}
