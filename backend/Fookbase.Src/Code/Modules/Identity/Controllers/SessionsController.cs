using System.Security.Claims;
using Fookbase.Api.Shared.Common;
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
        if (!User.TryGetUserId(out var userId))
        {
            return IdentityHttpHelpers.InvalidAccessToken();
        }
        Guid.TryParse(User.FindFirstValue("sid"), out var sessionId);
        var result = await authenticationService.GetSessionsAsync(
            userId, sessionId == Guid.Empty ? null : sessionId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpDelete("{sessionId:guid}")]
    [Authorize]
    public async Task<IResult> RevokeSessionAsync(
        [FromRoute] Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return IdentityHttpHelpers.InvalidAccessToken();
        }
        var result = await authenticationService.RevokeSessionAsync(
            userId, sessionId, TimeProvider.System.GetUtcNow(), cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    [HttpPost("revoke-others")]
    [Authorize]
    public async Task<IResult> RevokeOtherSessionsAsync(
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return IdentityHttpHelpers.InvalidAccessToken();
        }
        Guid.TryParse(User.FindFirstValue("sid"), out var currentSessionId);
        await authenticationService.RevokeOtherSessionsAsync(
            userId,
            currentSessionId == Guid.Empty ? null : currentSessionId,
            TimeProvider.System.GetUtcNow(),
            cancellationToken);
        return Results.NoContent();
    }
}
