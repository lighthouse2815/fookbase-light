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
