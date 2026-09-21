namespace Fookbase.Api.Modules.Posts.DTOs.Responses;

public sealed record ContentReportResponse(
    Guid Id,
    string TargetType,
    Guid TargetId,
    string Reason,
    string? Details,
    string Status,
    DateTimeOffset CreatedAtUtc);
