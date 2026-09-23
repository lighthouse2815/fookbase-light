using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Reels.DTOs.Requests;
using Fookbase.Api.Modules.Reels.Services;

namespace Fookbase.Api.Modules.Reels.Endpoints;

public static class ReelEndpoints
{
    public static IEndpointRouteBuilder MapReelEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reels");
        group.MapPost("", CreateAsync).RequireAuthorization();
        group.MapGet("", GetFeedAsync).RequireAuthorization();
        group.MapGet("/{reelId:guid}", GetAsync);
        group.MapGet("/{reelId:guid}/video/access", GetVideoAccessAsync).RequireAuthorization();
        group.MapGet("/{reelId:guid}/poster/access", GetPosterAccessAsync).RequireAuthorization();
        group.MapPost("/{reelId:guid}/views", RecordViewAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateReelRequest request, ClaimsPrincipal principal,
        ReelsService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.CreateAsync(actorUserId, request.Caption, request.Privacy,
            request.VideoMediaId, cancellationToken);
        return result.Succeeded ? Results.Created("/api/reels/" + result.Value!.Id, result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetFeedAsync(ClaimsPrincipal principal, ReelsService service,
        CancellationToken cancellationToken, string? mode = null, string? cursor = null,
        int limit = ReelsService.DefaultPageSize)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.GetFeedAsync(actorUserId, mode, cursor, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetAsync(Guid reelId, ClaimsPrincipal principal, ReelsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetViewerUserId(principal, out var viewerUserId)) return Results.Unauthorized();
        var result = await service.GetAsync(viewerUserId, reelId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static Task<IResult> GetVideoAccessAsync(Guid reelId, ClaimsPrincipal principal,
        ReelsService service, CancellationToken cancellationToken) =>
        GetMediaAccessAsync(reelId, false, principal, service, cancellationToken);

    private static Task<IResult> GetPosterAccessAsync(Guid reelId, ClaimsPrincipal principal,
        ReelsService service, CancellationToken cancellationToken) =>
        GetMediaAccessAsync(reelId, true, principal, service, cancellationToken);

    private static async Task<IResult> GetMediaAccessAsync(Guid reelId, bool poster,
        ClaimsPrincipal principal, ReelsService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.GetMediaAccessAsync(actorUserId, reelId, poster, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> RecordViewAsync(Guid reelId, CreateReelViewRequest request,
        ClaimsPrincipal principal, ReelsService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.RecordViewAsync(actorUserId, reelId, request.WatchDurationMs,
            request.Completed, request.Replayed, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static bool TryGetViewerUserId(ClaimsPrincipal principal, out Guid? userId)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            userId = null;
            return true;
        }

        var parsed = TryGetActorUserId(principal, out var actorUserId);
        userId = parsed ? actorUserId : null;
        return parsed;
    }

    private static bool TryGetActorUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
