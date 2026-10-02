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
        var result = await authenticationService.LoginAsync(
            request,
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        return Results.Ok(ApiResponse.Success(AuthenticationCookie.Present(HttpContext, result), HttpContext));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IResult> RefreshAsync(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        var token = AuthenticationCookie.ReadRefreshToken(Request, request.RefreshToken);
        var result = await authenticationService.RefreshAsync(new RefreshRequest(token), cancellationToken);

        return Results.Ok(ApiResponse.Success(AuthenticationCookie.Present(HttpContext, result), HttpContext));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IResult> LogoutAsync(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var token = AuthenticationCookie.ReadRefreshToken(Request, request.RefreshToken);
        await authenticationService.LogoutAsync(
            userId,
            new LogoutRequest(token),
            cancellationToken);

        AuthenticationCookie.Clear(HttpContext);

        return Results.Ok(ApiResponse.Success(HttpContext));
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

        return Results.Ok(ApiResponse.Success(result, HttpContext));
    }
}
