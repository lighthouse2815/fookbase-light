using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fookbase.Api.Modules.Identity.Controllers;

[Route("api/auth/email")]
public sealed class EmailVerificationController(
    AuthenticationService authenticationService) : IdentityControllerBase
{
    [HttpPost("verify")]
    [AllowAnonymous]
    public async Task<IResult> VerifyEmailAsync(
        [FromBody] VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.VerifyEmailAsync(request, cancellationToken);

        return result.Succeeded
            ? Results.NoContent()
            : result.Error!.ToHttpResult();
    }

    [HttpPost("verification")]
    [Authorize]
    [EnableRateLimiting("auth-resend-verification")]
    public async Task<IResult> SendEmailVerificationAsync(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return InvalidAccessToken();
        }

        var result = await authenticationService.SendEmailVerificationAsync(userId, cancellationToken);

        return result.Succeeded
            ? Results.NoContent()
            : result.Error!.ToHttpResult();
    }
}
