using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Media.DTOs.Requests;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Media.Controllers;

[ApiController]
[Authorize]
[Route("api/media")]
public sealed class MediaController(MediaService service) : ControllerBase
{
    [HttpPost("uploads")]
    public async Task<IActionResult> CreateUploadAsync(
        [FromBody] CreateUploadRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var ownerUserId)) return Unauthorized();
        var result = await service.CreateUploadAsync(ownerUserId, request, cancellationToken);
        return result.Succeeded
            ? Created($"/api/media/{result.Value!.MediaId}", result.Value)
            : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpPost("{mediaId:guid}/complete")]
    public async Task<IActionResult> CompleteAsync(
        Guid mediaId, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var ownerUserId)) return Unauthorized();
        var result = await service.CompleteAsync(ownerUserId, mediaId, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpGet("{mediaId:guid}")]
    public async Task<IActionResult> GetMetadataAsync(
        Guid mediaId, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var ownerUserId)) return Unauthorized();
        var result = await service.GetMetadataAsync(ownerUserId, mediaId, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpDelete("{mediaId:guid}")]
    public async Task<IActionResult> DeleteAsync(
        Guid mediaId, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var ownerUserId)) return Unauthorized();
        var result = await service.DeleteAsync(ownerUserId, mediaId, cancellationToken);
        return result.Succeeded ? NoContent() : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpGet("{mediaId:guid}/access")]
    public async Task<IActionResult> GetOwnerReadUrlAsync(
        Guid mediaId, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var ownerUserId)) return Unauthorized();
        var result = await service.CreateOwnerReadUrlAsync(ownerUserId, mediaId, false, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpGet("{mediaId:guid}/poster/access")]
    public async Task<IActionResult> GetOwnerPosterReadUrlAsync(
        Guid mediaId, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var ownerUserId)) return Unauthorized();
        var result = await service.CreateOwnerReadUrlAsync(ownerUserId, mediaId, true, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    private static bool TryGetActorUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
