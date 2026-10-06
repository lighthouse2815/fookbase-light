using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Reels.DTOs.Requests;
using Fookbase.Api.Modules.Reels.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Reels.Controllers;

[ApiController]
[Authorize]
[Route("api/reels")]
public sealed class ReelsController(ReelsService service) : ControllerBase
{
    [HttpPost]
    public async Task<IResult> CreateAsync(
        [FromBody] CreateReelRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Results.Unauthorized();
        var result = await service.CreateAsync(actorUserId, request.Caption, request.Privacy,
            request.VideoMediaId, cancellationToken);
        return result.Succeeded ? Results.Created("/api/reels/" + result.Value!.Id, result.Value)
            : result.Error!.ToHttpResult();
    }

    [HttpGet]
    public async Task<IResult> GetFeedAsync(
        [FromQuery] ReelFeedRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Results.Unauthorized();
        var result = await service.GetFeedAsync(actorUserId, request.Mode, request.Cursor, request.Limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("{reelId:guid}")]
    [AllowAnonymous]
    public async Task<IResult> GetAsync(
        [FromRoute] Guid reelId,
        CancellationToken cancellationToken)
    {
        if (!TryGetViewerUserId(User, out var viewerUserId)) return Results.Unauthorized();
        var result = await service.GetAsync(viewerUserId, reelId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("{reelId:guid}/video/access")]
    public Task<IResult> GetVideoAccessAsync(
        [FromRoute] Guid reelId,
        CancellationToken cancellationToken) =>
        GetMediaAccessAsync(reelId, false, cancellationToken);

    [HttpGet("{reelId:guid}/poster/access")]
    public Task<IResult> GetPosterAccessAsync(
        [FromRoute] Guid reelId,
        CancellationToken cancellationToken) =>
        GetMediaAccessAsync(reelId, true, cancellationToken);

    [HttpPost("{reelId:guid}/views")]
    public async Task<IResult> RecordViewAsync(
        [FromRoute] Guid reelId,
        [FromBody] CreateReelViewRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Results.Unauthorized();
        var result = await service.RecordViewAsync(actorUserId, reelId, request.WatchDurationMs,
            request.Completed, request.Replayed, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private async Task<IResult> GetMediaAccessAsync(Guid reelId, bool poster, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Results.Unauthorized();
        var result = await service.GetMediaAccessAsync(actorUserId, reelId, poster, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
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
