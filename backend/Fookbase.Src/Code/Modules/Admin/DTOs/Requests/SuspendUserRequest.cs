using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Admin.Entities;
using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Admin.DTOs.Requests;

[CustomValidation(typeof(SuspendUserRequest), nameof(ValidateSuspension))]
public sealed record SuspendUserRequest(
    [TrimmedStringLength(ModerationAction.MaximumReasonLength, MinimumLength = 1,
        ErrorMessage = "Lý do phải có từ {2} đến {1} ký tự.")]
    string? Reason,
    int? DurationHours,
    DateTimeOffset? SuspendedUntilUtc,

    [TrimmedStringLength(ModerationAction.MaximumInternalNoteLength,
        ErrorMessage = "Ghi chú nội bộ không được vượt quá {1} ký tự.")]
    string? InternalNote)
{
    public const int MaximumSuspensionHours = 24 * 365;

    public static ValidationResult? ValidateSuspension(SuspendUserRequest request, ValidationContext context)
    {
        if (request.SuspendedUntilUtc is { } until)
        {
            var timeProvider = context.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System;
            var now = timeProvider.GetUtcNow();
            return until > now && until <= now.AddHours(MaximumSuspensionHours)
                ? ValidationResult.Success
                : new ValidationResult("Thời điểm kết thúc đình chỉ phải trong tương lai và không quá 365 ngày.",
                    [nameof(SuspendedUntilUtc)]);
        }

        return request.DurationHours is >= 1 and <= MaximumSuspensionHours
            ? ValidationResult.Success
            : new ValidationResult($"Thời gian đình chỉ phải từ 1 đến {MaximumSuspensionHours} giờ.",
                [nameof(DurationHours)]);
    }
}
