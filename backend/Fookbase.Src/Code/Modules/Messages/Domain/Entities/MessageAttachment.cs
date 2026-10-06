using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Messages.Entities;

[PrimaryKey(nameof(MessageId), nameof(MediaId))]
[Index(nameof(MediaId))]
[Index(nameof(MessageId), nameof(SortOrder), IsUnique = true)]
public sealed class MessageAttachment
{
    private MessageAttachment() { }

    public MessageAttachment(Guid messageId, Guid mediaId, int sortOrder)
    {
        MessageId = messageId;
        MediaId = mediaId;
        SortOrder = sortOrder;
    }

    public Guid MessageId { get; private set; }
    public Guid MediaId { get; private set; }
    public int SortOrder { get; private set; }

    [InverseProperty(nameof(Message.Attachments))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Message Message { get; private set; } = null!;

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset Media { get; private set; } = null!;
}
