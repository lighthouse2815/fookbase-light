using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Admin.DTOs.Responses;
using Fookbase.Api.Modules.Admin.DTOs.Requests;
using Fookbase.Api.Modules.Admin.Services;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Posts.DTOs.Requests;
using Fookbase.Api.Modules.Posts.Services;

namespace Fookbase.Api.Modules.Admin.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin").RequireAuthorization(AdminPolicy.Name);
        group.MapGet("/dashboard", GetDashboardAsync);
        group.MapGet("/users", GetUsersAsync);
        group.MapPatch("/users/{userId:guid}/status", UpdateUserStatusAsync);
        group.MapGet("/reports", GetReportsAsync);
        group.MapPatch("/reports/{reportId:guid}/status", UpdateReportStatusAsync);
        group.MapGet("/reports/{reportId:guid}", GetReportAsync);
        group.MapPost("/reports/{reportId:guid}/dismiss", DismissReportAsync);
        group.MapPost("/reports/{reportId:guid}/remove-content", RemoveReportedContentAsync);
        group.MapPost("/reports/{reportId:guid}/warn-user", WarnReportedUserAsync);
        group.MapPost("/reports/{reportId:guid}/suspend-user", SuspendReportedUserAsync);
        group.MapGet("/users/{userId:guid}/moderation-state", GetModerationStateAsync);
        group.MapGet("/users/{userId:guid}/moderation-history", GetModerationHistoryAsync);
        group.MapPost("/users/{userId:guid}/warn", WarnUserAsync);
        group.MapPost("/users/{userId:guid}/suspend", SuspendUserAsync);
        group.MapPost("/users/{userId:guid}/unsuspend", UnsuspendUserAsync);
        group.MapPost("/users/{userId:guid}/disable", DisableUserAsync);
        group.MapPost("/users/{userId:guid}/enable", EnableUserAsync);
        group.MapDelete("/posts/{postId:guid}", DeletePostAsync);
        return endpoints;
    }

    private static async Task<AdminDashboardResponse> GetDashboardAsync(
        AdministrationUseCase useCase,
        CancellationToken cancellationToken) =>
        await useCase.GetDashboardAsync(cancellationToken);

    private static async Task<IResult> GetUsersAsync(
        string? query,
        AdministrationService administrationService,
        CancellationToken cancellationToken,
        int offset = 0,
        int limit = 20)
    {
        var result = await administrationService.SearchUsersAsync(query, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> UpdateUserStatusAsync(
        Guid userId,
        UpdateUserStatusRequest request,
        ClaimsPrincipal principal,
        AdministrationService administrationService,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await administrationService.UpdateUserStatusAsync(
            actorUserId, userId, request.IsActive, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetReportsAsync(
        string? status,
        string? targetType,
        string? cursor,
        ReportsService reportsService,
        ModerationService moderationService,
        CancellationToken cancellationToken,
        int offset = 0,
        int limit = 20)
    {
        if (!string.IsNullOrWhiteSpace(cursor) || !string.IsNullOrWhiteSpace(targetType))
        {
            var queue = await moderationService.GetQueueAsync(status, targetType, cursor, limit, cancellationToken);
            return queue.Succeeded ? Results.Ok(queue.Value) : queue.Error!.ToHttpResult();
        }
        var result = await reportsService.GetReportsAsync(status, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> UpdateReportStatusAsync(
        Guid reportId,
        UpdateReportStatusRequest request,
        ReportsService reportsService,
        CancellationToken cancellationToken)
    {
        var result = await reportsService.UpdateReportStatusAsync(reportId, request.Status, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> DeletePostAsync(
        Guid postId,
        AdministrationUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.DeletePostAsync(postId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetReportAsync(Guid reportId, ModerationService moderationService, CancellationToken cancellationToken)
    {
        var result = await moderationService.GetReportAsync(reportId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> DismissReportAsync(Guid reportId, DismissReportRequest request, ClaimsPrincipal principal, ModerationService service, CancellationToken cancellationToken) =>
        await WithModeratorAsync(principal, actor => service.DismissReportAsync(actor, reportId, request.Reason, request.InternalNote, cancellationToken));

    private static async Task<IResult> RemoveReportedContentAsync(Guid reportId, ModerationReasonRequest request, ClaimsPrincipal principal, ModerationService service, CancellationToken cancellationToken) =>
        await WithModeratorAsync(principal, actor => service.RemoveReportedContentAsync(actor, reportId, request.Reason, request.InternalNote, cancellationToken));

    private static async Task<IResult> WarnReportedUserAsync(Guid reportId, ModerationReasonRequest request, ClaimsPrincipal principal, ModerationService service, CancellationToken cancellationToken) =>
        await WithModeratorAsync(principal, actor => service.WarnReportedUserAsync(actor, reportId, request.Reason, request.InternalNote, cancellationToken));

    private static async Task<IResult> SuspendReportedUserAsync(Guid reportId, SuspendUserRequest request, ClaimsPrincipal principal, ModerationService service, CancellationToken cancellationToken)
    {
        var detail = await service.GetReportAsync(reportId, cancellationToken);
        if (!detail.Succeeded) return detail.Error!.ToHttpResult();
        if (detail.Value!.SubjectUserId is not { } userId) return Results.NotFound();
        return await WithModeratorAsync(principal, actor => service.SuspendUserAsync(actor, userId, request.Reason, request.DurationHours, request.SuspendedUntilUtc, request.InternalNote, cancellationToken));
    }

    private static async Task<IResult> GetModerationStateAsync(Guid userId, ModerationService service, CancellationToken cancellationToken)
    {
        var result = await service.GetStateAsync(userId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetModerationHistoryAsync(Guid userId, ModerationService service, CancellationToken cancellationToken, string? cursor = null, int limit = 20)
    {
        var result = await service.GetHistoryAsync(userId, cursor, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> WarnUserAsync(Guid userId, ModerationReasonRequest request, ClaimsPrincipal principal, ModerationService service, CancellationToken cancellationToken) =>
        await WithModeratorAsync(principal, actor => service.WarnUserAsync(actor, userId, request.Reason, request.InternalNote, cancellationToken));

    private static async Task<IResult> SuspendUserAsync(Guid userId, SuspendUserRequest request, ClaimsPrincipal principal, ModerationService service, CancellationToken cancellationToken) =>
        await WithModeratorAsync(principal, actor => service.SuspendUserAsync(actor, userId, request.Reason, request.DurationHours, request.SuspendedUntilUtc, request.InternalNote, cancellationToken));

    private static async Task<IResult> UnsuspendUserAsync(Guid userId, ModerationReasonRequest request, ClaimsPrincipal principal, ModerationService service, CancellationToken cancellationToken) =>
        await WithModeratorAsync(principal, actor => service.UnsuspendUserAsync(actor, userId, request.Reason, request.InternalNote, cancellationToken));

    private static async Task<IResult> DisableUserAsync(Guid userId, ModerationReasonRequest request, ClaimsPrincipal principal, ModerationService service, CancellationToken cancellationToken) =>
        await WithModeratorAsync(principal, actor => service.DisableUserAsync(actor, userId, request.Reason, request.InternalNote, cancellationToken));

    private static async Task<IResult> EnableUserAsync(Guid userId, ModerationReasonRequest request, ClaimsPrincipal principal, ModerationService service, CancellationToken cancellationToken) =>
        await WithModeratorAsync(principal, actor => service.EnableUserAsync(actor, userId, request.Reason, request.InternalNote, cancellationToken));

    private static async Task<IResult> WithModeratorAsync<T>(ClaimsPrincipal principal, Func<Guid, Task<ApplicationResult<T>>> action)
    {
        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
        var result = await action(userId);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
