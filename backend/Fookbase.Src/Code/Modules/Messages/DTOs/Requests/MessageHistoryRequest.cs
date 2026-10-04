using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Messages.Common;
using Fookbase.Api.Modules.Messages.Services;

namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record MessageHistoryRequest(
    [CustomValidation(typeof(MessageHistoryRequest), nameof(MessageHistoryRequest.ValidateCursor))]
    string? Before = null,

    [Range(1, MessagesService.MaximumLimit)]
    int Limit = 50)
{
    public static ValidationResult? ValidateCursor(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
            value.Length <= MessageCursorCodec.MaximumLength && MessageCursorCodec.TryDecode<MessageCursor>(value)
            ? ValidationResult.Success
            : new ValidationResult("Con trỏ phân trang không hợp lệ.");
}
