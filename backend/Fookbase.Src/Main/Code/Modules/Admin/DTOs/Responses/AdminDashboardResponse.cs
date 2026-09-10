namespace Fookbase.Api.Modules.Admin.DTOs.Responses;

public sealed record AdminDashboardResponse(
    int TotalUsers,
    int ActiveUsers,
    int ActivePosts,
    int PendingReports);
