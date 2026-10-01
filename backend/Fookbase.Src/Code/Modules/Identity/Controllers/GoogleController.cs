using Fookbase.Api.Shared.ErrorHandling;
using Fookbase.Api.Shared.Common;
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

[ApiController]
[Route("api/auth")]
public sealed class GoogleController(
    GoogleAuthenticationOptions googleOptions,
    IGoogleExternalIdentityReader identityReader,
    GoogleAuthenticationService googleAuthentication) : ControllerBase
{
    [HttpGet("providers")]
    [AllowAnonymous]
    public IResult GetExternalProviders([FromQuery] string? client = null) =>
        Results.Ok(ApiResponse.Success(new ExternalAuthenticationProvidersResponse(googleOptions.Enabled,
            googleOptions.Enabled && (client is null
                ? googleOptions.IsMobileClientEnabled(IdentityModuleConstants.ExternalLogin.Clients.Mobile) ||
                  googleOptions.IsMobileClientEnabled(IdentityModuleConstants.ExternalLogin.Clients.ZolaMobile)
                : googleOptions.IsMobileClientEnabled(client))), HttpContext));

    [HttpGet("google/start")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    public IResult StartGoogle(
        [FromQuery] string? client)
    {
        if (!googleOptions.Enabled)
        {
            throw GoogleUnavailable();
        }

        if (!IsSupportedGoogleClient(client))
        {
            throw InvalidGoogleClient();
        }

        return Results.Challenge(
            new AuthenticationProperties
            {
                RedirectUri = $"/api/auth/google/callback?client={Uri.EscapeDataString(client!)}"
            },
            [IdentityModuleConstants.ExternalLogin.GoogleScheme]);
    }

    [HttpGet("google/callback")]
    [AllowAnonymous]
    public async Task<IResult> CompleteGoogleCallbackAsync(
        [FromQuery] string? client,
        CancellationToken cancellationToken)
    {
        if (!googleOptions.Enabled)
        {
            throw GoogleUnavailable();
        }

        if (!IsSupportedGoogleClient(client))
        {
            throw InvalidGoogleClient();
        }

        var identity = await identityReader.ReadAsync(HttpContext, cancellationToken);
        if (identity.Client is not null)
        {
            // Native OAuth state must only be redeemed through its PKCE-bound callback.
            throw InvalidGoogleClient();
        }

        var completion = await googleAuthentication.CreateCompletionAsync(
            client!,
            identity.ProviderKey,
            identity.Email,
            identity.EmailVerified,
            cancellationToken);

        var target = googleOptions.GetClientLoginUri(client!);
        var query = new Dictionary<string, string?>
        {
            ["provider"] = "google",
            ["code"] = completion.Code,
            ["mode"] = completion.RequiresPassword ? "link" : null,
            ["email"] = completion.RequiresPassword ? completion.Email : null
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
            throw GoogleUnavailable();
        }

        if (!IsSupportedGoogleClient(request.Client))
        {
            throw InvalidGoogleClient();
        }

        var result = await googleAuthentication.ExchangeAsync(
            request.Code ?? string.Empty,
            request.Client!,
            Request.Headers.UserAgent.ToString(),
            cancellationToken);
        return Results.Ok(ApiResponse.Success(AuthenticationCookie.Present(HttpContext, result), HttpContext));
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
            throw GoogleUnavailable();
        }

        if (!IsSupportedGoogleClient(request.Client))
        {
            throw InvalidGoogleClient();
        }

        var result = await googleAuthentication.LinkExistingAsync(
            request.Code ?? string.Empty,
            request.Client!,
            request.Password ?? string.Empty,
            Request.Headers.UserAgent.ToString(),
            cancellationToken);
        return Results.Ok(ApiResponse.Success(AuthenticationCookie.Present(HttpContext, result), HttpContext));
    }

    private static bool IsSupportedGoogleClient(string? client) => client is
        IdentityModuleConstants.ExternalLogin.Clients.Web or IdentityModuleConstants.ExternalLogin.Clients.ZolaLight;

    private static BusinessException InvalidGoogleClient() => new(new ApplicationError(
        "invalid_google_client",
        "The Google authentication client is unsupported.",
        ApplicationErrorType.Validation));

    private static BusinessException GoogleUnavailable() => new(new ApplicationError(
        "google_unavailable",
        "Google authentication is unavailable.",
        ApplicationErrorType.NotFound));
}
