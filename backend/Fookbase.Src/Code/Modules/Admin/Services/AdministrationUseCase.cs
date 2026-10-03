using Fookbase.Api.Modules.Admin.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Posts.Services;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Admin.Services;

public sealed class AdministrationUseCase(
    AdministrationService administrationService,
    ReportsService reportsService,
    PostsService postsService,
    MediaService mediaService,
    FookbaseDbContext dbContext)
{
    public async Task<AdminDashboardResponse> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        var totalUsers = await administrationService.CountUsersAsync(cancellationToken);
        var activeUsers = await administrationService.CountActiveUsersAsync(cancellationToken);
        var moderation = await reportsService.GetModerationSummaryAsync(cancellationToken);
        var today = DateTimeOffset.UtcNow.UtcDateTime.Date;
        var start = new DateTimeOffset(today.AddDays(-29), TimeSpan.Zero);
        var end = new DateTimeOffset(today.AddDays(1), TimeSpan.Zero);

        // Aggregate in PostgreSQL and return every UTC day, including days without activity.
        // Keep queries sequential because the services share the same DbContext.
        var usersByDay = await dbContext.Users
            .Where(user => user.CreatedAt >= start && user.CreatedAt < end)
            .GroupBy(user => user.CreatedAt.DateTime.Date)
            .Select(group => new { Date = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Date, item => item.Count, cancellationToken);
        var postsByDay = await dbContext.Posts
            .Where(post => post.CreatedAtUtc >= start && post.CreatedAtUtc < end && post.DeletedAtUtc == null)
            .GroupBy(post => post.CreatedAtUtc.DateTime.Date)
            .Select(group => new { Date = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Date, item => item.Count, cancellationToken);
        var reportsByDay = await dbContext.ContentReports
            .Where(report => report.CreatedAtUtc >= start && report.CreatedAtUtc < end)
            .GroupBy(report => report.CreatedAtUtc.DateTime.Date)
            .Select(group => new { Date = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Date, item => item.Count, cancellationToken);
        var reportsByStatus = await dbContext.ContentReports
            .GroupBy(report => report.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);

        return new AdminDashboardResponse(
            totalUsers,
            activeUsers,
            moderation.ActivePostCount,
            moderation.PendingReportCount)
        {
            Activity = Enumerable.Range(0, 30).Select(offset =>
            {
                var date = start.UtcDateTime.AddDays(offset);
                return new AdminDailyActivity(DateOnly.FromDateTime(date),
                    usersByDay.GetValueOrDefault(date), postsByDay.GetValueOrDefault(date),
                    reportsByDay.GetValueOrDefault(date));
            }).ToArray(),
            ReportStatuses = Enum.GetValues<ContentReportStatus>()
                .Select(status => new AdminReportStatusCount(status.ToString().ToLowerInvariant(),
                    reportsByStatus.GetValueOrDefault(status))).ToArray()
        };
    }

    public async Task<ApplicationResult> DeletePostAsync(
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await postsService.DeletePostForModerationAsync(postId, cancellationToken);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return result;
            }

            await mediaService.RemovePostReferencesAsync(postId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ApplicationResult.Success();
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
