using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fookbase.Api.Modules.Identity.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class TwoFactorController(
    AuthenticationService authenticationService) : ControllerBase
{
    [HttpPost("2fa/verify")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    public async Task<IResult> VerifyTwoFactorAsync(
        [FromBody] TwoFactorVerifyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.VerifyTwoFactorAsync(request, null, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("security")]
    [Authorize]
    public async Task<IResult> GetSecurityAsync(
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return IdentityHttpHelpers.InvalidAccessToken();
        }
        var result = await authenticationService.GetSecurityAsync(userId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPost("2fa/setup")]
    [Authorize]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IResult> SetupTwoFactorAsync(
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return IdentityHttpHelpers.InvalidAccessToken();
        }
        var result = await authenticationService.SetupTwoFactorAsync(userId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPost("2fa/enable")]
    [Authorize]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IResult> EnableTwoFactorAsync(
        [FromBody] TwoFactorCodeRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return IdentityHttpHelpers.InvalidAccessToken();
        }
        var result = await authenticationService.EnableTwoFactorAsync(userId, request.Code, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPost("2fa/disable")]
    [Authorize]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IResult> DisableTwoFactorAsync(
        [FromBody] DisableTwoFactorRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return IdentityHttpHelpers.InvalidAccessToken();
        }
        var result = await authenticationService.DisableTwoFactorAsync(userId, request.CurrentPassword, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    [HttpPost("2fa/recovery-codes/regenerate")]
    [Authorize]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IResult> RegenerateRecoveryCodesAsync(
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return IdentityHttpHelpers.InvalidAccessToken();
        }
        var result = await authenticationService.RegenerateRecoveryCodesAsync(userId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }
}
