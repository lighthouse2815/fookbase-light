using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Admin.DTOs.Requests;
using Fookbase.Api.Modules.Posts.Domain.Enums;

namespace Fookbase.Api.Modules.Admin.Common;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ReportStatusUpdateAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is null ||
        value is string text &&
        Enum.TryParse<ContentReportStatus>(text, true, out var status) &&
        Enum.IsDefined(status) && status != ContentReportStatus.PENDING;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ValidModerationCursorAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null) return true;
        if (value is not string text) return false;

        try
        {
            _ = ModerationCursor.DecodeOrNull(text);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class ValidSuspensionAttribute : ValidationAttribute
{
    private const int MaximumSuspensionHours = 24 * 365;

    public override bool RequiresValidationContext => true;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null) return ValidationResult.Success;

        var request = (SuspendUserRequest)value;
        if (request.SuspendedUntilUtc is { } until)
        {
            var timeProvider = validationContext.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System;
            var now = timeProvider.GetUtcNow();
            return until > now && until <= now.AddHours(MaximumSuspensionHours)
                ? ValidationResult.Success
                : new ValidationResult("Thời điểm kết thúc đình chỉ phải trong tương lai và không quá 365 ngày.",
                    [nameof(SuspendUserRequest.SuspendedUntilUtc)]);
        }

        return request.DurationHours is >= 1 and <= MaximumSuspensionHours
            ? ValidationResult.Success
            : new ValidationResult($"Thời gian đình chỉ phải từ 1 đến {MaximumSuspensionHours} giờ.",
                [nameof(SuspendUserRequest.DurationHours)]);
    }
}
