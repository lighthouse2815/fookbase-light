using System.Security.Claims;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Identity.Controllers;

[ApiController]
[Route("api/auth/sessions")]
public sealed class SessionsController(
    AuthenticationService authenticationService) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IResult> GetSessionsAsync(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        Guid.TryParse(User.FindFirstValue("sid"), out var sessionId);
        var result = await authenticationService.GetSessionsAsync(
            userId, sessionId == Guid.Empty ? null : sessionId, cancellationToken);
        return Results.Ok(result);
    }

    [HttpDelete("{sessionId:guid}")]
    [Authorize]
    public async Task<IResult> RevokeSessionAsync(
        [FromRoute] Guid sessionId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await authenticationService.RevokeSessionAsync(
            userId, sessionId, TimeProvider.System.GetUtcNow(), cancellationToken);
        return Results.NoContent();
    }

    [HttpPost("revoke-others")]
    [Authorize]
    public async Task<IResult> RevokeOtherSessionsAsync(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        Guid.TryParse(User.FindFirstValue("sid"), out var currentSessionId);
        await authenticationService.RevokeOtherSessionsAsync(
            userId,
            currentSessionId == Guid.Empty ? null : currentSessionId,
            TimeProvider.System.GetUtcNow(),
            cancellationToken);
        return Results.NoContent();
    }
}
