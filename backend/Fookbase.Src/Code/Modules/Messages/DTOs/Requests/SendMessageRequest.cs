using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Messages.Common;
using Fookbase.Api.Modules.Messages.Entities;

namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

[MessageContent(ErrorMessage = "Tin nhắn cần nội dung hoặc tệp đính kèm.")]
public sealed record SendMessageRequest(
    [TrimmedStringLength(Message.MaximumContentLength)]
    string? Content,

    [MaxLength(SendMessageRequest.MaximumAttachmentCount)]
    [DistinctMessageIds(ErrorMessage = "Danh sách ID phải khác rỗng và không được trùng nhau.")]
    IReadOnlyList<Guid>? MediaIds = null,

    Guid? ReplyToMessageId = null)
{
    public const int MaximumAttachmentCount = 10;
}
