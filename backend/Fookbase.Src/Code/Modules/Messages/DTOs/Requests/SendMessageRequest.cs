using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Messages.Common;
using Fookbase.Api.Modules.Messages.Entities;

namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record SendMessageRequest(
    [TrimmedStringLength(Message.MaximumContentLength)]
    string? Content,

    [MaxLength(SendMessageRequest.MaximumAttachmentCount)]
    [CustomValidation(typeof(MessageRequestValidation), nameof(MessageRequestValidation.ValidateDistinctIds))]
    IReadOnlyList<Guid>? MediaIds = null,

    Guid? ReplyToMessageId = null) : IValidatableObject
{
    public const int MaximumAttachmentCount = 10;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Content) && (MediaIds is null || MediaIds.Count == 0))
        {
            yield return new ValidationResult("Tin nhắn cần nội dung hoặc tệp đính kèm.",
                [nameof(Content), nameof(MediaIds)]);
        }
    }
}
