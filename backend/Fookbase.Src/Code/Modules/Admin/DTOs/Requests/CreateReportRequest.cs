using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Admin.Domain.Enums;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Admin.DTOs.Requests;

public sealed record CreateReportRequest(
    [Required(ErrorMessage = "Lý do báo cáo là bắt buộc.")]
    [OptionalEnumValue<ReportReason>(ErrorMessage = "Lý do báo cáo không hợp lệ.")]
    string? Reason,

    [TrimmedStringLength(500, ErrorMessage = "Chi tiết báo cáo không được vượt quá {1} ký tự.")]
    string? Details);
