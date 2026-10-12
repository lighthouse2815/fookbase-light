namespace Fookbase.Api.Modules.Admin.DTOs.Responses;

public sealed record ModerationSummaryResponse(
    int ActivePostCount,
    int PendingReportCount);
