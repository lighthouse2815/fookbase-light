using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Friends.Common;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Identity.Common;
using Microsoft.AspNetCore.DataProtection;

namespace Fookbase.Api.Modules.Friends.DTOs.Requests;

public sealed record FollowPageRequest(
    [CustomValidation(typeof(FollowPageRequest), nameof(FollowPageRequest.ValidateCursor))]
    string? Cursor = null,

    [Range(1, FriendsService.MaximumFollowPageSize, ErrorMessage = "Số kết quả phải từ {1} đến {2}.")]
    int Limit = FriendsService.DefaultFollowPageSize)
{
    public static ValidationResult? ValidateCursor(string? value, ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(value)) return ValidationResult.Success;
        var httpContext = ((IHttpContextAccessor)context.GetService(typeof(IHttpContextAccessor))!).HttpContext!;
        var protectionProvider = httpContext.RequestServices.GetRequiredService<IDataProtectionProvider>();
        var direction = httpContext.Request.RouteValues["action"] is string action &&
            action.StartsWith("GetFollowers", StringComparison.Ordinal) ? "followers" : "following";
        try
        {
            var targetUserId = Guid.Parse(httpContext.Request.RouteValues["userId"]!.ToString()!);
            _ = FollowCursor.DecodeOrNull(value, httpContext.User.GetUserId(), direction, targetUserId, protectionProvider);
            return ValidationResult.Success;
        }
        catch (FormatException)
        {
            return new ValidationResult("Con trỏ danh sách theo dõi không hợp lệ.");
        }
    }
}
