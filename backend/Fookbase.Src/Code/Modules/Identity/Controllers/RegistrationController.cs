using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fookbase.Api.Modules.Identity.Controllers;

[ApiController]
[Route("api/auth/registration")]
public sealed class RegistrationController(
    RegistrationChallengeService registrationChallengeService,
    RegistrationUseCase registrationUseCase) : ControllerBase
{
    [HttpPost("/api/auth/register")]
    [AllowAnonymous]
    public async Task<IResult> RegisterAsync(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await registrationUseCase.ExecuteAsync(request, cancellationToken);

        return Results.Created("/api/auth/me", ApiResponse.Success(result, HttpContext));
    }

    [HttpPost("start")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IResult> StartRegistrationAsync(
        [FromBody] RegistrationStartRequest request,
        CancellationToken cancellationToken)
    {
        var result = await registrationChallengeService.StartAsync(request, cancellationToken);
        return Results.Accepted($"/api/auth/registration/{result.ChallengeId}", ApiResponse.Success(result, HttpContext));
    }

    [HttpPost("resend")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IResult> ResendRegistrationAsync(
        [FromBody] RegistrationResendRequest request,
        CancellationToken cancellationToken)
    {
        var result = await registrationChallengeService.ResendAsync(request, cancellationToken);
        return Results.Ok(ApiResponse.Success(result, HttpContext));
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
        return Results.Created("/api/auth/me", ApiResponse.Success(result, HttpContext));
    }
}
