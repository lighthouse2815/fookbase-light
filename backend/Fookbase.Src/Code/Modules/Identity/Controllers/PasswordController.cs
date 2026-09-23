using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fookbase.Api.Modules.Identity.Controllers;

[Route("api/auth/password")]
public sealed class PasswordController(
    AuthenticationService authenticationService) : IdentityControllerBase
{
    [HttpPost("forgot")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-password-recovery")]
    public async Task<IResult> RequestPasswordResetAsync(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.RequestPasswordResetAsync(request, cancellationToken);

        return result.Succeeded
            ? Results.NoContent()
            : result.Error!.ToHttpResult();
    }

    [HttpPost("reset")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-password-recovery")]
    public async Task<IResult> ResetPasswordAsync(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.ResetPasswordAsync(request, cancellationToken);

        return result.Succeeded
            ? Results.NoContent()
            : result.Error!.ToHttpResult();
    }

    [HttpPost("change")]
    [Authorize]
    public async Task<IResult> ChangePasswordAsync(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return InvalidAccessToken();
        }

        var result = await authenticationService.ChangePasswordAsync(userId, request, cancellationToken);

        return result.Succeeded
            ? Results.Ok(result.Value)
            : result.Error!.ToHttpResult();
    }
}
