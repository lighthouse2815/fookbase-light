namespace Fookbase.Api.Modules.Admin.DTOs.Responses;

public sealed record AdminDashboardResponse(
    int TotalUsers,
    int ActiveUsers,
    int ActivePosts,
    int PendingReports)
{
    public IReadOnlyList<AdminDailyActivity> Activity { get; init; } = [];
    public IReadOnlyList<AdminReportStatusCount> ReportStatuses { get; init; } = [];
}

public sealed record AdminDailyActivity(DateOnly Date, int NewUsers, int NewPosts, int NewReports);

public sealed record AdminReportStatusCount(string Status, int Count);
