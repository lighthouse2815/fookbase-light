using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Users.DTOs.Requests;
using Fookbase.Api.Modules.Users.Services;
using Fookbase.Api.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Users.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UserProfileController(
    UserProfileService profileService,
    MediaService mediaService) : ControllerBase
{
    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<IResult> SearchAsync(
        [FromQuery] string? query,
        CancellationToken cancellationToken,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20)
    {
        var result = await profileService.SearchAsync(query, offset, limit, GetViewerUserId(), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("{userId:guid}")]
    [AllowAnonymous]
    public async Task<IResult> GetByIdAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await profileService.GetAsync(userId, GetViewerUserId(), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("{userId:guid}/avatar")]
    [AllowAnonymous]
    public Task<IResult> GetAvatarAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken) =>
        GetProfileMediaAsync(
            () => profileService.GetAvatarMediaIdAsync(userId, cancellationToken), cancellationToken);

    [HttpGet("{userId:guid}/cover")]
    [AllowAnonymous]
    public Task<IResult> GetCoverAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken) =>
        GetProfileMediaAsync(
            () => profileService.GetCoverMediaIdAsync(userId, cancellationToken), cancellationToken);

    [HttpGet("me")]
    [Authorize]
    public async Task<IResult> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await profileService.GetAsync(userId, userId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPatch("me")]
    [Authorize]
    public async Task<IResult> UpdateCurrentAsync(
        [FromBody] UpdateUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await profileService.UpdateAsync(User.GetUserId(), request, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private Guid? GetViewerUserId() =>
        Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId) ? userId : null;

    private async Task<IResult> GetProfileMediaAsync(
        Func<Task<Guid?>> getMediaId,
        CancellationToken cancellationToken)
    {
        var mediaId = await getMediaId();
        if (mediaId is null)
        {
            return Results.NotFound();
        }

        var readUrl = await mediaService.CreateReadUrlAsync(mediaId.Value, cancellationToken);
        return readUrl.Succeeded ? Results.Redirect(readUrl.Value!.Url) : Results.NotFound();
    }
}
