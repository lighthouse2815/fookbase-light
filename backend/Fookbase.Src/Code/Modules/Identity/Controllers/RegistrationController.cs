using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fookbase.Api.Modules.Identity.Controllers;

[Route("api/auth/registration")]
public sealed class RegistrationController(
    RegistrationChallengeService registrationChallengeService,
    RegistrationUseCase registrationUseCase) : IdentityControllerBase
{
    [HttpPost("/api/auth/register")]
    [AllowAnonymous]
    public async Task<IResult> RegisterAsync(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await registrationUseCase.ExecuteAsync(request, cancellationToken);

        return result.Succeeded
            ? Results.Created("/api/auth/me", result.Value)
            : result.Error!.ToHttpResult();
    }

    [HttpPost("start")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IResult> StartRegistrationAsync(
        [FromBody] RegistrationStartRequest request,
        CancellationToken cancellationToken)
    {
        var result = await registrationChallengeService.StartAsync(request, cancellationToken);
        return result.Succeeded
            ? Results.Accepted($"/api/auth/registration/{result.Value!.ChallengeId}", result.Value)
            : result.Error!.ToHttpResult();
    }

    [HttpPost("resend")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IResult> ResendRegistrationAsync(
        [FromBody] RegistrationResendRequest request,
        CancellationToken cancellationToken)
    {
        var result = await registrationChallengeService.ResendAsync(request, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPost("verify")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IResult> VerifyRegistrationAsync(
        [FromBody] RegistrationVerifyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await registrationChallengeService.VerifyAsync(
            request,
            Request.Headers.UserAgent.ToString(),
            cancellationToken);
        return result.Succeeded
            ? Results.Created("/api/auth/me", result.Value)
            : result.Error!.ToHttpResult();
    }
}
