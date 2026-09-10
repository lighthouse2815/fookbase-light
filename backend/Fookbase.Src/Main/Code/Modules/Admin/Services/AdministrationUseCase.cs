using Fookbase.Api.Modules.Admin.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Posts.Common;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Persistence;

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

        return new AdminDashboardResponse(
            totalUsers,
            activeUsers,
            moderation.ActivePostCount,
            moderation.PendingReportCount);
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
