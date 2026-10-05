using Fookbase.Api.Modules.Admin.DTOs.Requests;
using Fookbase.Api.Modules.Admin.DTOs.Responses;
using Fookbase.Api.Modules.Admin.Services;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Admin.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AdminPolicy.Name)]
public sealed class AdminController(
    AdministrationUseCase useCase,
    AdministrationService administrationService,
    ReportsService reportsService,
    ModerationService moderationService) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken) =>
        await useCase.GetDashboardAsync(cancellationToken);

    [HttpGet("users")]
    public async Task<IResult> GetUsersAsync(
        [FromQuery] AdminUserPageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await administrationService.SearchUsersAsync(request.Query, request.Offset, request.Limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPatch("users/{userId:guid}/status")]
    public async Task<IResult> UpdateUserStatusAsync(
        [FromRoute] Guid userId,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await administrationService.UpdateUserStatusAsync(
            User.GetUserId(), userId, request.IsActive!.Value, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("reports")]
    public async Task<IResult> GetReportsAsync(
        [FromQuery] AdminReportPageRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Cursor) || !string.IsNullOrWhiteSpace(request.TargetType))
        {
            var queue = await moderationService.GetQueueAsync(request.Status, request.TargetType, request.Cursor, request.Limit, cancellationToken);
            return queue.Succeeded ? Results.Ok(queue.Value) : queue.Error!.ToHttpResult();
        }

        var result = await reportsService.GetReportsAsync(request.Status, request.Offset, request.Limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPatch("reports/{reportId:guid}/status")]
    public async Task<IResult> UpdateReportStatusAsync(
        [FromRoute] Guid reportId,
        [FromBody] UpdateReportStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await reportsService.UpdateReportStatusAsync(reportId, request.Status, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("reports/{reportId:guid}")]
    public async Task<IResult> GetReportAsync([FromRoute] Guid reportId, CancellationToken cancellationToken)
    {
        var result = await moderationService.GetReportAsync(reportId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPost("reports/{reportId:guid}/dismiss")]
    public async Task<IResult> DismissReportAsync(
        [FromRoute] Guid reportId,
        [FromBody] DismissReportRequest request,
        CancellationToken cancellationToken)
    {
        var result = await moderationService.DismissReportAsync(
            User.GetUserId(), reportId, request.Reason, request.InternalNote, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPost("reports/{reportId:guid}/remove-content")]
    public async Task<IResult> RemoveReportedContentAsync(
        [FromRoute] Guid reportId,
        [FromBody] ModerationReasonRequest request,
        CancellationToken cancellationToken)
    {
        var result = await moderationService.RemoveReportedContentAsync(
            User.GetUserId(), reportId, request.Reason, request.InternalNote, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPost("reports/{reportId:guid}/warn-user")]
    public async Task<IResult> WarnReportedUserAsync(
        [FromRoute] Guid reportId,
        [FromBody] ModerationReasonRequest request,
        CancellationToken cancellationToken)
    {
        var result = await moderationService.WarnReportedUserAsync(
            User.GetUserId(), reportId, request.Reason, request.InternalNote, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPost("reports/{reportId:guid}/suspend-user")]
    public async Task<IResult> SuspendReportedUserAsync(
        [FromRoute] Guid reportId,
        [FromBody] SuspendUserRequest request,
        CancellationToken cancellationToken)
    {
        var moderatorUserId = User.GetUserId();
        var detail = await moderationService.GetReportAsync(reportId, cancellationToken);
        if (!detail.Succeeded) return detail.Error!.ToHttpResult();
        if (detail.Value!.SubjectUserId is not { } userId) return Results.NotFound();
        var result = await moderationService.SuspendUserAsync(moderatorUserId, userId,
            request.Reason, request.DurationHours, request.SuspendedUntilUtc, request.InternalNote, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("users/{userId:guid}/moderation-state")]
    public async Task<IResult> GetModerationStateAsync([FromRoute] Guid userId, CancellationToken cancellationToken)
    {
        var result = await moderationService.GetStateAsync(userId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("users/{userId:guid}/moderation-history")]
    public async Task<IResult> GetModerationHistoryAsync(
        [FromRoute] Guid userId,
        [FromQuery] ModerationHistoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await moderationService.GetHistoryAsync(userId, request.Cursor, request.Limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPost("users/{userId:guid}/warn")]
    public async Task<IResult> WarnUserAsync(
        [FromRoute] Guid userId,
        [FromBody] ModerationReasonRequest request,
        CancellationToken cancellationToken)
    {
        var result = await moderationService.WarnUserAsync(
            User.GetUserId(), userId, request.Reason, request.InternalNote, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPost("users/{userId:guid}/suspend")]
    public async Task<IResult> SuspendUserAsync(
        [FromRoute] Guid userId,
        [FromBody] SuspendUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await moderationService.SuspendUserAsync(User.GetUserId(), userId,
            request.Reason, request.DurationHours, request.SuspendedUntilUtc, request.InternalNote, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPost("users/{userId:guid}/unsuspend")]
    public async Task<IResult> UnsuspendUserAsync(
        [FromRoute] Guid userId,
        [FromBody] ModerationReasonRequest request,
        CancellationToken cancellationToken)
    {
        var result = await moderationService.UnsuspendUserAsync(
            User.GetUserId(), userId, request.Reason, request.InternalNote, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPost("users/{userId:guid}/disable")]
    public async Task<IResult> DisableUserAsync(
        [FromRoute] Guid userId,
        [FromBody] ModerationReasonRequest request,
        CancellationToken cancellationToken)
    {
        var result = await moderationService.DisableUserAsync(
            User.GetUserId(), userId, request.Reason, request.InternalNote, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPost("users/{userId:guid}/enable")]
    public async Task<IResult> EnableUserAsync(
        [FromRoute] Guid userId,
        [FromBody] ModerationReasonRequest request,
        CancellationToken cancellationToken)
    {
        var result = await moderationService.EnableUserAsync(
            User.GetUserId(), userId, request.Reason, request.InternalNote, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpDelete("posts/{postId:guid}")]
    public async Task<IResult> DeletePostAsync([FromRoute] Guid postId, CancellationToken cancellationToken)
    {
        var result = await useCase.DeletePostAsync(postId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }
}
