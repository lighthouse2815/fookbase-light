using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Admin.Common;
using Fookbase.Api.Modules.Admin.Services;

namespace Fookbase.Api.Modules.Admin.DTOs.Requests;

public sealed record ModerationHistoryRequest(
    [ValidModerationCursor(ErrorMessage = "Con trỏ kiểm duyệt không hợp lệ.")]
    string? Cursor = null,

    [Range(1, ModerationService.MaximumPageSize, ErrorMessage = "Số lịch sử kiểm duyệt phải từ {1} đến {2}.")]
    int Limit = 20);
