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
