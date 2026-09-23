using Fookbase.Api.Shared.Common;
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
        return Results.Ok(ApiResponse.Success(result, HttpContext));
    }

    [HttpGet("security")]
    [Authorize]
    public async Task<IResult> GetSecurityAsync(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await authenticationService.GetSecurityAsync(userId, cancellationToken);
        return Results.Ok(ApiResponse.Success(result, HttpContext));
    }

    [HttpPost("2fa/setup")]
    [Authorize]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IResult> SetupTwoFactorAsync(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await authenticationService.SetupTwoFactorAsync(userId, cancellationToken);
        return Results.Ok(ApiResponse.Success(result, HttpContext));
    }

    [HttpPost("2fa/enable")]
    [Authorize]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IResult> EnableTwoFactorAsync(
        [FromBody] TwoFactorCodeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await authenticationService.EnableTwoFactorAsync(userId, request.Code, cancellationToken);
        return Results.Ok(ApiResponse.Success(result, HttpContext));
    }

    [HttpPost("2fa/disable")]
    [Authorize]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IResult> DisableTwoFactorAsync(
        [FromBody] DisableTwoFactorRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await authenticationService.DisableTwoFactorAsync(userId, request.CurrentPassword, cancellationToken);
        return Results.Ok(ApiResponse.Success(HttpContext));
    }

    [HttpPost("2fa/recovery-codes/regenerate")]
    [Authorize]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IResult> RegenerateRecoveryCodesAsync(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await authenticationService.RegenerateRecoveryCodesAsync(userId, cancellationToken);
        return Results.Ok(ApiResponse.Success(result, HttpContext));
    }
}
