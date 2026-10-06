using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Admin.Common;

namespace Fookbase.Api.Modules.Admin.DTOs.Requests;

public sealed record UpdateReportStatusRequest(
    [Required(ErrorMessage = "Trạng thái báo cáo là bắt buộc.")]
    [ReportStatusUpdate(ErrorMessage = "Trạng thái báo cáo phải là reviewed, resolved hoặc dismissed.")]
    string Status);
