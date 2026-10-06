using Fookbase.Api.Modules.Admin.Common;
using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Admin.DTOs.Requests;

[ValidSuspension]
public sealed record SuspendUserRequest(
    [TrimmedStringLength(500, MinimumLength = 1,
        ErrorMessage = "Lý do phải có từ {2} đến {1} ký tự.")]
    string? Reason,
    int? DurationHours,
    DateTimeOffset? SuspendedUntilUtc,

    [TrimmedStringLength(2_000,
        ErrorMessage = "Ghi chú nội bộ không được vượt quá {1} ký tự.")]
    string? InternalNote);
