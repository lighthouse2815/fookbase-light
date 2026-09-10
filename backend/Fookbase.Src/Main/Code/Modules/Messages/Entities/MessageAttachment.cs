namespace Fookbase.Api.Modules.Messages.Entities;

public sealed class MessageAttachment
{
    private MessageAttachment()
    {
    }

    private MessageAttachment(Guid messageId, Guid mediaId, int sortOrder)
    {
        MessageId = messageId;
        MediaId = mediaId;
        SortOrder = sortOrder;
    }

    public Guid MessageId { get; private set; }
    public Guid MediaId { get; private set; }
    public int SortOrder { get; private set; }

    public static MessageAttachment Create(Guid messageId, Guid mediaId, int sortOrder) =>
        new(messageId, mediaId, sortOrder);
}
