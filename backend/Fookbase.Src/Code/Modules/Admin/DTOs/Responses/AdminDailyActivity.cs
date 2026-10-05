namespace Fookbase.Api.Modules.Admin.DTOs.Responses;

public sealed record AdminDailyActivity(DateOnly Date, int NewUsers, int NewPosts, int NewReports);
