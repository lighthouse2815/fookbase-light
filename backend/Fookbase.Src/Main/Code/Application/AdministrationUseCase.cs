using Fookbase.Api.Modules.Admin.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Posts.Common;
using Fookbase.Api.Modules.Posts.Services;

namespace Fookbase.Api.Application;

public sealed class AdministrationUseCase(
    AdministrationService administrationService,
    ReportsService reportsService,
    PostsService postsService,
    MediaService mediaService)
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
        var result = await postsService.DeletePostForModerationAsync(postId, cancellationToken);
        if (!result.Succeeded)
        {
            return result;
        }

        await mediaService.RemovePostReferencesAsync(postId, cancellationToken);
        return ApplicationResult.Success();
    }
}
