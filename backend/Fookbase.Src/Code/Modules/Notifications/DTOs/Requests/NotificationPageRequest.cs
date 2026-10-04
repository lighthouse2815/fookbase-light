using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Notifications.Common;
using Fookbase.Api.Modules.Notifications.Services;

namespace Fookbase.Api.Modules.Notifications.DTOs.Requests;

public sealed record NotificationPageRequest(
    [CustomValidation(typeof(NotificationPageRequest), nameof(NotificationPageRequest.ValidateCursor))]
    string? Before = null,

    [Range(1, NotificationService.MaximumPageSize, ErrorMessage = "Số thông báo phải từ {1} đến {2}.")]
    int Limit = NotificationService.DefaultPageSize)
{
    public static ValidationResult? ValidateCursor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Success;
        }

        try
        {
            _ = NotificationCursor.Decode(value);
            return ValidationResult.Success;
        }
        catch (FormatException)
        {
            return new ValidationResult("Con trỏ thông báo không hợp lệ.");
        }
    }
}
