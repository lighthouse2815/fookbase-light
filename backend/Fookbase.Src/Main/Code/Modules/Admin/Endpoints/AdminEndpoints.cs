using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Admin.DTOs.Responses;
using Fookbase.Api.Modules.Admin.Services;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Posts.Common;
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
        ReportsService reportsService,
        CancellationToken cancellationToken,
        int offset = 0,
        int limit = 20)
    {
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

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
