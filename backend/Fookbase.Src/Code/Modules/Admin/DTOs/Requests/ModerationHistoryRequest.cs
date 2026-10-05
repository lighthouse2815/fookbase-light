using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Admin.Common;
using Fookbase.Api.Modules.Admin.Services;

namespace Fookbase.Api.Modules.Admin.DTOs.Requests;

public sealed record ModerationHistoryRequest(
    [CustomValidation(typeof(ModerationHistoryRequest), nameof(ModerationHistoryRequest.ValidateCursor))]
    string? Cursor = null,

    [Range(1, ModerationService.MaximumPageSize, ErrorMessage = "Số lịch sử kiểm duyệt phải từ {1} đến {2}.")]
    int Limit = 20)
{
    public static ValidationResult? ValidateCursor(string? value)
    {
        try
        {
            _ = ModerationCursor.DecodeOrNull(value);
            return ValidationResult.Success;
        }
        catch (FormatException)
        {
            return new ValidationResult("Con trỏ kiểm duyệt không hợp lệ.");
        }
    }
}
