namespace Fookbase.Api.Modules.Admin.DTOs.Responses;

public sealed record ModerationReportResponse(
    Guid Id,
    Guid ReporterUserId,
    string TargetType,
    Guid TargetId,
    string Reason,
    string? Details,
    string Status,
    DateTimeOffset CreatedAtUtc);
