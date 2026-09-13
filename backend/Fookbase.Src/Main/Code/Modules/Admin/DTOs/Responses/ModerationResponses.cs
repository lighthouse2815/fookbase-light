namespace Fookbase.Api.Modules.Admin.DTOs.Responses;

public sealed record ModerationActionResponse(
    Guid Id,
    Guid? ReportId,
    Guid SubjectUserId,
    string TargetType,
    Guid TargetId,
    string ActionType,
    string Reason,
    string? InternalNote,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc);

public sealed record UserModerationStateResponse(
    Guid UserId,
    int WarningCount,
    DateTimeOffset? SuspendedUntilUtc,
    DateTimeOffset? DisabledAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record ReportDetailResponse(
    Guid Id,
    Guid ReporterUserId,
    string TargetType,
    Guid TargetId,
    string Reason,
    string? Details,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    string? TargetPreview,
    Guid? SubjectUserId,
    IReadOnlyList<ModerationActionResponse> RecentActions,
    int ReportCount);

public sealed record ModerationActionPageResponse(
    IReadOnlyList<ModerationActionResponse> Items,
    string? NextCursor);
