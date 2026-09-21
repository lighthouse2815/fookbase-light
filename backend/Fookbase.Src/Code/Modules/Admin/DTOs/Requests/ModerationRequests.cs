namespace Fookbase.Api.Modules.Admin.DTOs.Requests;

public sealed record DismissReportRequest(string? Reason, string? InternalNote);
public sealed record ModerationReasonRequest(string? Reason, string? InternalNote);
public sealed record SuspendUserRequest(string? Reason, int? DurationHours, DateTimeOffset? SuspendedUntilUtc, string? InternalNote);
