using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Admin.DTOs.Requests;

public sealed record DismissReportRequest(
    [TrimmedStringLength(500, MinimumLength = 1,
        ErrorMessage = "Lý do phải có từ {2} đến {1} ký tự.")]
    string? Reason,

    [TrimmedStringLength(2_000,
        ErrorMessage = "Ghi chú nội bộ không được vượt quá {1} ký tự.")]
    string? InternalNote);
