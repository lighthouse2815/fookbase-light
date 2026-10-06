using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Photos.DTOs.Requests;
using Fookbase.Api.Modules.Photos.Services;
using Fookbase.Api.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Photos.Controllers;

[ApiController]
[Route("api/albums")]
public sealed class PhotoAlbumsController(PhotosService service) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreatePhotoAlbumRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetActorUserId();
        if (actorId is null) return Unauthorized();
        var result = await service.CreateAsync(actorId.Value, request, cancellationToken);
        return result.Succeeded
            ? Created($"/api/albums/{result.Value!.Id}", result.Value)
            : Failure(result.Error!);
    }

    [HttpGet("{albumId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAsync([FromRoute] Guid albumId, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(albumId, GetActorUserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : Failure(result.Error!);
    }

    [HttpPatch("{albumId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateAsync(
        [FromRoute] Guid albumId,
        [FromBody] UpdatePhotoAlbumRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetActorUserId();
        if (actorId is null) return Unauthorized();
        var result = await service.UpdateAsync(actorId.Value, albumId, request, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : Failure(result.Error!);
    }

    [HttpDelete("{albumId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid albumId, CancellationToken cancellationToken)
    {
        var actorId = GetActorUserId();
        if (actorId is null) return Unauthorized();
        var result = await service.DeleteAsync(actorId.Value, albumId, cancellationToken);
        return result.Succeeded ? NoContent() : Failure(result.Error!);
    }

    [HttpPost("{albumId:guid}/media")]
    [Authorize]
    public async Task<IActionResult> AddMediaAsync(
        [FromRoute] Guid albumId,
        [FromBody] AddAlbumMediaRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetActorUserId();
        if (actorId is null) return Unauthorized();
        var result = await service.AddMediaAsync(actorId.Value, albumId, request.MediaId, cancellationToken);
        return result.Succeeded
            ? Created($"/api/albums/{albumId}/media/{request.MediaId}", result.Value)
            : Failure(result.Error!);
    }

    [HttpGet("{albumId:guid}/media")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMediaAsync(
        [FromRoute] Guid albumId,
        CancellationToken cancellationToken,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = PhotosService.DefaultPageSize)
    {
        var result = await service.GetMediaAsync(albumId, GetActorUserId(), cursor, limit, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : Failure(result.Error!);
    }

    [HttpGet("{albumId:guid}/media/{mediaId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPhotoAsync(
        [FromRoute] Guid albumId,
        [FromRoute] Guid mediaId,
        CancellationToken cancellationToken)
    {
        var result = await service.GetPhotoAsync(albumId, mediaId, GetActorUserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : Failure(result.Error!);
    }

    [HttpGet("{albumId:guid}/media/{mediaId:guid}/access")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPhotoAccessAsync(
        [FromRoute] Guid albumId,
        [FromRoute] Guid mediaId,
        CancellationToken cancellationToken)
    {
        var result = await service.GetPhotoAsync(albumId, mediaId, GetActorUserId(), cancellationToken);
        return result.Succeeded ? Redirect(result.Value!.Url) : Failure(result.Error!);
    }

    [HttpPatch("{albumId:guid}/media/{mediaId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateCaptionAsync(
        [FromRoute] Guid albumId,
        [FromRoute] Guid mediaId,
        [FromBody] UpdateAlbumMediaRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetActorUserId();
        if (actorId is null) return Unauthorized();
        var result = await service.UpdateCaptionAsync(actorId.Value, albumId, mediaId, request, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : Failure(result.Error!);
    }

    [HttpDelete("{albumId:guid}/media/{mediaId:guid}")]
    [Authorize]
    public async Task<IActionResult> RemoveMediaAsync(
        [FromRoute] Guid albumId,
        [FromRoute] Guid mediaId,
        CancellationToken cancellationToken)
    {
        var actorId = GetActorUserId();
        if (actorId is null) return Unauthorized();
        var result = await service.RemoveMediaAsync(actorId.Value, albumId, mediaId, cancellationToken);
        return result.Succeeded ? NoContent() : Failure(result.Error!);
    }

    [HttpGet("/api/users/{userId:guid}/albums")]
    [AllowAnonymous]
    public async Task<IActionResult> GetUserAlbumsAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = PhotosService.DefaultPageSize)
    {
        var result = await service.GetUserAlbumsAsync(userId, GetActorUserId(), cursor, limit, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : Failure(result.Error!);
    }

    private static ObjectResult Failure(ApplicationError error) =>
        new(error.ToProblemDetails()) { StatusCode = error.ToStatusCode() };

    private Guid? GetActorUserId() =>
        Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId) ? userId : null;
}
