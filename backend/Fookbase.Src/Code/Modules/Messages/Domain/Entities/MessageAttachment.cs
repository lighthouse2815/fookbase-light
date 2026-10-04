namespace Fookbase.Api.Modules.Messages.Entities;

public sealed class MessageAttachment
{
    private MessageAttachment()
    {
    }

    public MessageAttachment(Guid messageId, Guid mediaId, int sortOrder)
    {
        MessageId = messageId;
        MediaId = mediaId;
        SortOrder = sortOrder;
    }

    public Guid MessageId { get; private set; }
    public Guid MediaId { get; private set; }
    public int SortOrder { get; private set; }

}
