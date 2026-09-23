using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fookbase.Api.Modules.Identity.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthenticationController(
    AuthenticationService authenticationService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    public async Task<IResult> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.LoginAsync(request, null, cancellationToken);

        return Results.Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IResult> RefreshAsync(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.RefreshAsync(request, cancellationToken);

        return Results.Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IResult> LogoutAsync(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await authenticationService.LogoutAsync(
            userId,
            request,
            cancellationToken);

        return Results.NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IResult> GetCurrentUserAsync(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var result = await authenticationService.GetCurrentUserAsync(
            userId,
            cancellationToken);

        return Results.Ok(result);
    }
}
