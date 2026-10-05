namespace Fookbase.Api.Modules.Admin.DTOs.Responses;

public sealed record ModerationQueuePageResponse(
    IReadOnlyList<ModerationQueueReportResponse> Items,
    string? NextCursor);
