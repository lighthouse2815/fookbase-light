namespace Fookbase.Api.Modules.Admin.DTOs.Responses;

public sealed record ModerationQueueReportResponse(
    Guid Id,
    string TargetType,
    Guid TargetId,
    string Reason,
    string? Details,
    string Status,
    DateTimeOffset CreatedAtUtc,
    string? TargetPreview,
    Guid? SubjectUserId,
    int ReportCount);

public sealed record ModerationQueuePageResponse(
    IReadOnlyList<ModerationQueueReportResponse> Items,
    string? NextCursor);
