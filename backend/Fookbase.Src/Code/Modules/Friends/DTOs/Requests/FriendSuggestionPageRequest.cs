using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Friends.Common;
using Fookbase.Api.Modules.Friends.Config;
using Fookbase.Api.Modules.Identity.Common;
using Microsoft.AspNetCore.DataProtection;

namespace Fookbase.Api.Modules.Friends.DTOs.Requests;

public sealed record FriendSuggestionPageRequest(
    [CustomValidation(typeof(FriendSuggestionPageRequest), nameof(FriendSuggestionPageRequest.ValidateCursor))]
    string? Cursor = null,

    [Range(1, 50, ErrorMessage = "Số kết quả phải từ {1} đến {2}.")]
    [CustomValidation(typeof(FriendSuggestionPageRequest), nameof(FriendSuggestionPageRequest.ValidateLimit))]
    int? Limit = null)
{
    public static ValidationResult? ValidateLimit(int? value, ValidationContext context)
    {
        var options = (FriendSuggestionOptions)context.GetService(typeof(FriendSuggestionOptions))!;
        return value is null || value <= options.MaximumPageSize
            ? ValidationResult.Success
            : new ValidationResult($"Số kết quả không được vượt quá {options.MaximumPageSize}.");
    }

    public static ValidationResult? ValidateCursor(string? value, ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(value)) return ValidationResult.Success;
        var httpContext = ((IHttpContextAccessor)context.GetService(typeof(IHttpContextAccessor))!).HttpContext!;
        var protectionProvider = httpContext.RequestServices.GetRequiredService<IDataProtectionProvider>();
        try
        {
            _ = FriendSuggestionCursor.DecodeOrNull(value, httpContext.User.GetUserId(), protectionProvider);
            return ValidationResult.Success;
        }
        catch (FormatException)
        {
            return new ValidationResult("Con trỏ gợi ý kết bạn không hợp lệ.");
        }
    }
}
