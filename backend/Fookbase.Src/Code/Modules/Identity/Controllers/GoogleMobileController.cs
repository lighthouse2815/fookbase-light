using Fookbase.Api.Shared.ErrorHandling;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Identity.Config;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace Fookbase.Api.Modules.Identity.Controllers;

[ApiController]
[Route("api/auth/google/mobile")]
public sealed class GoogleMobileController(
    GoogleAuthenticationOptions options,
    IGoogleExternalIdentityReader reader,
    GoogleAuthenticationService service,
    GoogleMobileFlow flow) : ControllerBase
{
    [HttpGet("start")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    public IResult Start(
        [FromQuery] string? challenge,
        [FromQuery] string? state)
    {
        if (!Enabled(options))
        {
            return Results.NotFound();
        }
        if (!GoogleMobileFlow.IsValidChallenge(challenge) || !GoogleMobileFlow.IsValidState(state))
        {
            throw Invalid();
        }
        var properties = new AuthenticationProperties { RedirectUri = "/api/auth/google/mobile/callback" };
        properties.Items["fookbase.client"] = "mobile";
        properties.Items["fookbase.code_challenge"] = challenge;
        properties.Items["fookbase.state"] = state;
        return Results.Challenge(properties, ["Google"]);
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    public async Task<IResult> CallbackAsync(
        CancellationToken cancellationToken)
    {
        if (!Enabled(options))
        {
            return Results.NotFound();
        }
        var identity = await reader.ReadAsync(HttpContext, cancellationToken, mobile: true);
        if (identity.Client != "mobile" ||
            !GoogleMobileFlow.IsValidChallenge(identity.CodeChallenge) ||
            !GoogleMobileFlow.IsValidState(identity.State))
        {
            throw Invalid();
        }

        var completion = await service.CreateCompletionAsync(
            "mobile",
            identity.ProviderKey,
            identity.Email,
            identity.EmailVerified,
            cancellationToken);
        return Results.Redirect(QueryHelpers.AddQueryString(options.MobileCallbackUrl, new Dictionary<string, string?>
        {
            ["code"] = flow.Protect(completion.Code, identity.CodeChallenge!),
            ["state"] = identity.State,
            ["mode"] = completion.RequiresPassword ? "link" : "login"
        }));
    }

    [HttpPost("exchange")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    public async Task<IResult> ExchangeAsync(
        [FromBody] GoogleMobileCompletionRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enabled(options))
        {
            return Results.NotFound();
        }
        var code = flow.Unprotect(request.Code, request.Verifier);
        if (code is null)
        {
            throw Invalid();
        }
        var result = await service.ExchangeAsync(
            code, "mobile", Request.Headers.UserAgent.ToString(), cancellationToken);
        return Results.Ok(result);
    }

    [HttpPost("link")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    public async Task<IResult> LinkAsync(
        [FromBody] GoogleMobileCompletionRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enabled(options))
        {
            return Results.NotFound();
        }
        var code = flow.Unprotect(request.Code, request.Verifier);
        if (code is null)
        {
            throw Invalid();
        }
        var result = await service.LinkExistingAsync(
            code,
            "mobile",
            request.Password ?? string.Empty,
            Request.Headers.UserAgent.ToString(),
            cancellationToken);
        return Results.Ok(result);
    }

    private static bool Enabled(GoogleAuthenticationOptions options) =>
        options.Enabled && !string.IsNullOrWhiteSpace(options.MobileCallbackUrl);

    private static BusinessException Invalid() => new(new ApplicationError(
        "invalid_mobile_google_login",
        "The mobile Google sign-in is invalid or expired.",
        ApplicationErrorType.Unauthorized));
}
