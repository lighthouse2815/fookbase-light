namespace Fookbase.Api.Modules.Admin.DTOs.Responses;

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
