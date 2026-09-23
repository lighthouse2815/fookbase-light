using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fookbase.Api.Modules.Identity.Controllers;

[ApiController]
[Route("api/auth/password")]
public sealed class PasswordController(
    AuthenticationService authenticationService) : ControllerBase
{
    [HttpPost("forgot")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-password-recovery")]
    public async Task<IResult> RequestPasswordResetAsync(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await authenticationService.RequestPasswordResetAsync(request, cancellationToken);

        return Results.Ok(ApiResponse.Success(HttpContext));
    }

    [HttpPost("reset")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-password-recovery")]
    public async Task<IResult> ResetPasswordAsync(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await authenticationService.ResetPasswordAsync(request, cancellationToken);

        return Results.Ok(ApiResponse.Success(HttpContext));
    }

    [HttpPost("change")]
    [Authorize]
    public async Task<IResult> ChangePasswordAsync(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var result = await authenticationService.ChangePasswordAsync(userId, request, cancellationToken);

        return Results.Ok(ApiResponse.Success(AuthenticationCookie.Present(HttpContext, result), HttpContext));
    }
}
