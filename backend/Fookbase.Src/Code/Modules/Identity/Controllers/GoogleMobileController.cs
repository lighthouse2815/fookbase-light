using Fookbase.Api.Modules.Identity.Common;
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
            return Invalid();
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
        var identity = await reader.ReadAsync(HttpContext, cancellationToken);
        if (!identity.Succeeded || identity.Value!.Client != "mobile" ||
            !GoogleMobileFlow.IsValidChallenge(identity.Value.CodeChallenge) ||
            !GoogleMobileFlow.IsValidState(identity.Value.State))
        {
            return Invalid();
        }

        var completion = await service.CreateCompletionAsync(
            "mobile",
            identity.Value.ProviderKey,
            identity.Value.Email,
            identity.Value.EmailVerified,
            cancellationToken);
        if (!completion.Succeeded)
        {
            return completion.Error!.ToHttpResult();
        }
        return Results.Redirect(QueryHelpers.AddQueryString(options.MobileCallbackUrl, new Dictionary<string, string?>
        {
            ["code"] = flow.Protect(completion.Value!.Code, identity.Value.CodeChallenge!),
            ["state"] = identity.Value.State,
            ["mode"] = completion.Value.RequiresPassword ? "link" : "login"
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
            return Invalid();
        }
        var result = await service.ExchangeAsync(
            code, "mobile", Request.Headers.UserAgent.ToString(), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
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
            return Invalid();
        }
        var result = await service.LinkExistingAsync(
            code,
            "mobile",
            request.Password ?? string.Empty,
            Request.Headers.UserAgent.ToString(),
            cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static bool Enabled(GoogleAuthenticationOptions options) =>
        options.Enabled && !string.IsNullOrWhiteSpace(options.MobileCallbackUrl);

    private static IResult Invalid() => new ApplicationError(
        "invalid_mobile_google_login",
        "The mobile Google sign-in is invalid or expired.",
        ApplicationErrorType.Unauthorized).ToHttpResult();
}
