using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Admin.Services;
using Fookbase.Api.Modules.Posts.Domain.Enums;

namespace Fookbase.Api.Modules.Admin.DTOs.Requests;

public sealed record AdminReportPageRequest(
    [CustomValidation(typeof(AdminReportPageRequest), nameof(AdminReportPageRequest.ValidateStatus))]
    string? Status = null,

    [CustomValidation(typeof(AdminReportPageRequest), nameof(AdminReportPageRequest.ValidateTargetType))]
    string? TargetType = null,

    [CustomValidation(typeof(ModerationHistoryRequest), nameof(ModerationHistoryRequest.ValidateCursor))]
    string? Cursor = null,

    [Range(0, int.MaxValue, ErrorMessage = "Vị trí bắt đầu không được âm.")]
    int Offset = 0,

    [Range(1, ModerationService.MaximumPageSize, ErrorMessage = "Số báo cáo phải từ {1} đến {2}.")]
    int Limit = 20)
{
    public static ValidationResult? ValidateStatus(string? value) =>
        string.IsNullOrWhiteSpace(value) || Enum.TryParse<ContentReportStatus>(value, true, out var status) && Enum.IsDefined(status)
            ? ValidationResult.Success
            : new ValidationResult("Trạng thái báo cáo phải là pending, reviewed, resolved hoặc dismissed.");

    public static ValidationResult? ValidateTargetType(string? value) =>
        string.IsNullOrWhiteSpace(value) || Enum.TryParse<ReportTargetType>(value, true, out var target) && Enum.IsDefined(target)
            ? ValidationResult.Success
            : new ValidationResult("Loại đối tượng báo cáo phải là user hoặc post.");
}
