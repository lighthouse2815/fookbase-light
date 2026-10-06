using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Admin.Common;
using Fookbase.Api.Modules.Admin.Services;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Admin.DTOs.Requests;

public sealed record AdminReportPageRequest(
    [OptionalEnumValue<ContentReportStatus>(
        ErrorMessage = "Trạng thái báo cáo phải là pending, reviewed, resolved hoặc dismissed.")]
    string? Status = null,

    [OptionalEnumValue<ReportTargetType>(
        ErrorMessage = "Loại đối tượng báo cáo phải là user hoặc post.")]
    string? TargetType = null,

    [ValidModerationCursor(ErrorMessage = "Con trỏ kiểm duyệt không hợp lệ.")]
    string? Cursor = null,

    [Range(0, int.MaxValue, ErrorMessage = "Vị trí bắt đầu không được âm.")]
    int Offset = 0,

    [Range(1, ModerationService.MaximumPageSize, ErrorMessage = "Số báo cáo phải từ {1} đến {2}.")]
    int Limit = 20);
