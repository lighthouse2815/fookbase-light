namespace Fookbase.Api.Modules.Posts.DTOs.Responses;

public sealed record ModerationSummaryResponse(
    int ActivePostCount,
    int PendingReportCount);
