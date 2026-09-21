namespace Fookbase.Api.Modules.Posts.DTOs.Responses;

public sealed record ModerationReportResponse(
    Guid Id,
    Guid ReporterUserId,
    string TargetType,
    Guid TargetId,
    string Reason,
    string? Details,
    string Status,
    DateTimeOffset CreatedAtUtc);
