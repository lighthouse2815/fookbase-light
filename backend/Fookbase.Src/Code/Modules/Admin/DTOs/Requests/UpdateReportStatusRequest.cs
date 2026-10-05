using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Posts.Domain.Enums;

namespace Fookbase.Api.Modules.Admin.DTOs.Requests;

public sealed record UpdateReportStatusRequest(
    [Required(ErrorMessage = "Trạng thái báo cáo là bắt buộc.")]
    [CustomValidation(typeof(UpdateReportStatusRequest), nameof(UpdateReportStatusRequest.ValidateStatus))]
    string Status)
{
    public static ValidationResult? ValidateStatus(string? value) =>
        value is null || Enum.TryParse<ContentReportStatus>(value, true, out var status) &&
        Enum.IsDefined(status) && status != ContentReportStatus.PENDING
            ? ValidationResult.Success
            : new ValidationResult("Trạng thái báo cáo phải là reviewed, resolved hoặc dismissed.");
}
