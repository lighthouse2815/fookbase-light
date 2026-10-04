using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Messages.Common;
using Fookbase.Api.Modules.Messages.Services;

namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record ConversationPageRequest(
    [CustomValidation(typeof(ConversationPageRequest), nameof(ConversationPageRequest.ValidateCursor))]
    string? Before = null,

    [Range(1, MessagesService.MaximumLimit)]
    int Limit = 20,
    bool IncludeArchived = false)
{
    public static ValidationResult? ValidateCursor(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
            value.Length <= MessageCursorCodec.MaximumLength && MessageCursorCodec.TryDecode<ConversationCursor>(value)
            ? ValidationResult.Success
            : new ValidationResult("Con trỏ phân trang không hợp lệ.");
}
