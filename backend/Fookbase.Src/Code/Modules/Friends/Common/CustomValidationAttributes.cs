using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Friends.Config;
using Fookbase.Api.Modules.Identity.Common;
using Microsoft.AspNetCore.DataProtection;

namespace Fookbase.Api.Modules.Friends.Common;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ValidFollowCursorAttribute : ValidationAttribute
{
    public override bool RequiresValidationContext => true;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var cursor = (string?)value;
        if (string.IsNullOrWhiteSpace(cursor)) return ValidationResult.Success;
        var httpContext = ((IHttpContextAccessor)validationContext.GetService(typeof(IHttpContextAccessor))!).HttpContext!;
        var protectionProvider = httpContext.RequestServices.GetRequiredService<IDataProtectionProvider>();
        var direction = httpContext.Request.RouteValues["action"] is string action &&
            action.StartsWith("GetFollowers", StringComparison.Ordinal) ? "followers" : "following";
        try
        {
            var targetUserId = Guid.Parse(httpContext.Request.RouteValues["userId"]!.ToString()!);
            _ = FollowCursor.DecodeOrNull(cursor, httpContext.User.GetUserId(), direction, targetUserId, protectionProvider);
            return ValidationResult.Success;
        }
        catch (FormatException)
        {
            return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
        }
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ValidFriendSuggestionCursorAttribute : ValidationAttribute
{
    public override bool RequiresValidationContext => true;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var cursor = (string?)value;
        if (string.IsNullOrWhiteSpace(cursor)) return ValidationResult.Success;
        var httpContext = ((IHttpContextAccessor)validationContext.GetService(typeof(IHttpContextAccessor))!).HttpContext!;
        var protectionProvider = httpContext.RequestServices.GetRequiredService<IDataProtectionProvider>();
        try
        {
            _ = FriendSuggestionCursor.DecodeOrNull(cursor, httpContext.User.GetUserId(), protectionProvider);
            return ValidationResult.Success;
        }
        catch (FormatException)
        {
            return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
        }
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class FriendSuggestionPageLimitAttribute : ValidationAttribute
{
    public override bool RequiresValidationContext => true;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var limit = (int?)value;
        var options = (FriendSuggestionOptions)validationContext.GetService(typeof(FriendSuggestionOptions))!;
        return limit is null || limit <= options.MaximumPageSize
            ? ValidationResult.Success
            : new ValidationResult($"Số kết quả không được vượt quá {options.MaximumPageSize}.");
    }
}
