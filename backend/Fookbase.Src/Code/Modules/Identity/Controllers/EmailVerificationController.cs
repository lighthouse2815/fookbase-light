using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fookbase.Api.Modules.Identity.Controllers;

[ApiController]
[Route("api/auth/email")]
public sealed class EmailVerificationController(
    AccountSecurityService accountSecurityService) : ControllerBase
{
    [HttpPost("verify")]
    [AllowAnonymous]
    public async Task<IResult> VerifyEmailAsync(
        [FromBody] VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        await accountSecurityService.VerifyEmailAsync(request, cancellationToken);

        return Results.Ok(ApiResponse.Success(HttpContext));
    }

    [HttpPost("verification")]
    [Authorize]
    [EnableRateLimiting("auth-resend-verification")]
    public async Task<IResult> SendEmailVerificationAsync(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await accountSecurityService.SendEmailVerificationAsync(userId, cancellationToken);

        return Results.Ok(ApiResponse.Success(HttpContext));
    }
}
