using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Config;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using AuthenticationProperties = Microsoft.AspNetCore.Authentication.AuthenticationProperties;

namespace Fookbase.Api.Modules.Identity.Controllers;

[Route("api/auth")]
public sealed class GoogleController(
    GoogleAuthenticationOptions googleOptions,
    IGoogleExternalIdentityReader identityReader,
    GoogleAuthenticationService googleAuthentication) : IdentityControllerBase
{
    [HttpGet("providers")]
    [AllowAnonymous]
    public IResult GetExternalProviders() =>
        Results.Ok(new ExternalAuthenticationProvidersResponse(googleOptions.Enabled,
            googleOptions.Enabled && !string.IsNullOrWhiteSpace(googleOptions.MobileCallbackUrl)));

    [HttpGet("google/start")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    public IResult StartGoogle(
        [FromQuery] string? client)
    {
        if (!googleOptions.Enabled)
        {
            return GoogleUnavailable();
        }

        if (!IsSupportedGoogleClient(client))
        {
            return InvalidGoogleClient();
        }

        return Results.Challenge(
            new AuthenticationProperties
            {
                RedirectUri = $"/api/auth/google/callback?client={Uri.EscapeDataString(client!)}"
            },
            ["Google"]);
    }

    [HttpGet("google/callback")]
    [AllowAnonymous]
    public async Task<IResult> CompleteGoogleCallbackAsync(
        [FromQuery] string? client,
        CancellationToken cancellationToken)
    {
        if (!googleOptions.Enabled)
        {
            return GoogleUnavailable();
        }

        if (!IsSupportedGoogleClient(client))
        {
            return InvalidGoogleClient();
        }

        var identity = await identityReader.ReadAsync(HttpContext, cancellationToken);
        if (!identity.Succeeded)
        {
            return identity.Error!.ToHttpResult();
        }
        if (identity.Value!.Client is not null)
        {
            // Native OAuth state must only be redeemed through its PKCE-bound callback.
            return InvalidGoogleClient();
        }

        var completion = await googleAuthentication.CreateCompletionAsync(
            client!,
            identity.Value!.ProviderKey,
            identity.Value.Email,
            identity.Value.EmailVerified,
            cancellationToken);
        if (!completion.Succeeded)
        {
            return completion.Error!.ToHttpResult();
        }

        var target = googleOptions.GetClientLoginUri(client!);
        var query = new Dictionary<string, string?>
        {
            ["provider"] = "google",
            ["code"] = completion.Value!.Code,
            ["mode"] = completion.Value.RequiresPassword ? "link" : null,
            ["email"] = completion.Value.RequiresPassword ? completion.Value.Email : null
        };
        return Results.Redirect(QueryHelpers.AddQueryString(target, query));
    }

    [HttpPost("google/exchange")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    public async Task<IResult> ExchangeGoogleCompletionAsync(
        [FromBody] GoogleCompletionRequest request,
        CancellationToken cancellationToken)
    {
        if (!googleOptions.Enabled)
        {
            return GoogleUnavailable();
        }

        if (!IsSupportedGoogleClient(request.Client))
        {
            return InvalidGoogleClient();
        }

        var result = await googleAuthentication.ExchangeAsync(
            request.Code ?? string.Empty,
            request.Client!,
            Request.Headers.UserAgent.ToString(),
            cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPost("google/link")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    public async Task<IResult> LinkGoogleAsync(
        [FromBody] GoogleLinkRequest request,
        CancellationToken cancellationToken)
    {
        if (!googleOptions.Enabled)
        {
            return GoogleUnavailable();
        }

        if (!IsSupportedGoogleClient(request.Client))
        {
            return InvalidGoogleClient();
        }

        var result = await googleAuthentication.LinkExistingAsync(
            request.Code ?? string.Empty,
            request.Client!,
            request.Password ?? string.Empty,
            Request.Headers.UserAgent.ToString(),
            cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static bool IsSupportedGoogleClient(string? client) => client is "web" or "zola-light";

    private static IResult InvalidGoogleClient() => new ApplicationError(
        "invalid_google_client",
        "The Google authentication client is unsupported.",
        ApplicationErrorType.Validation).ToHttpResult();

    private static IResult GoogleUnavailable() => new ApplicationError(
        "google_unavailable",
        "Google authentication is unavailable.",
        ApplicationErrorType.NotFound).ToHttpResult();
}
