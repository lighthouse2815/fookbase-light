using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Posts.Common;
using Fookbase.Api.Modules.Posts.DTOs.Requests;
using Fookbase.Api.Modules.Posts.Services;

namespace Fookbase.Api.Modules.Posts.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reports").RequireAuthorization();
        group.MapPost("/users/{userId:guid}", ReportUserAsync);
        group.MapPost("/posts/{postId:guid}", ReportPostAsync);
        return endpoints;
    }

    private static async Task<IResult> ReportUserAsync(
        Guid userId,
        CreateReportRequest request,
        ClaimsPrincipal principal,
        ReportsService reportsService,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await reportsService.ReportUserAsync(actorUserId, userId, request, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/reports/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> ReportPostAsync(
        Guid postId,
        CreateReportRequest request,
        ClaimsPrincipal principal,
        ReportsService reportsService,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await reportsService.ReportPostAsync(actorUserId, postId, request, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/reports/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    private static bool TryGetActorUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
