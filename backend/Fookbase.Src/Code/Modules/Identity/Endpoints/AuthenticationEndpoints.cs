using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Config;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using AuthenticationProperties = Microsoft.AspNetCore.Authentication.AuthenticationProperties;

namespace Fookbase.Api.Modules.Identity.Endpoints;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth");
        group.MapGoogleMobileEndpoints();

        group.MapGet("/providers", GetExternalProvidersAsync).AllowAnonymous();
        group.MapGet("/google/start", StartGoogleAsync).AllowAnonymous().RequireRateLimiting("auth-login");
        group.MapGet("/google/callback", CompleteGoogleCallbackAsync).AllowAnonymous();
        group.MapPost("/google/exchange", ExchangeGoogleCompletionAsync).AllowAnonymous().RequireRateLimiting("auth-login");
        group.MapPost("/google/link", LinkGoogleAsync).AllowAnonymous().RequireRateLimiting("auth-login");
        if (endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>().IsEnvironment("Testing"))
        {
            group.MapPost("/register", RegisterAsync).AllowAnonymous();
        }
        group.MapPost("/registration/start", StartRegistrationAsync).AllowAnonymous().RequireRateLimiting("auth-sensitive");
        group.MapPost("/registration/resend", ResendRegistrationAsync).AllowAnonymous().RequireRateLimiting("auth-sensitive");
        group.MapPost("/registration/verify", VerifyRegistrationAsync).AllowAnonymous().RequireRateLimiting("auth-sensitive");
        group.MapPost("/login", LoginAsync).AllowAnonymous().RequireRateLimiting("auth-login");
        group.MapPost("/2fa/verify", VerifyTwoFactorAsync).AllowAnonymous().RequireRateLimiting("auth-login");
        group.MapPost("/refresh", RefreshAsync).AllowAnonymous();
        group.MapPost("/password/forgot", RequestPasswordResetAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-password-recovery");
        group.MapPost("/password/reset", ResetPasswordAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-password-recovery");
        group.MapPost("/email/verify", VerifyEmailAsync).AllowAnonymous();
        group.MapPost("/logout", LogoutAsync).RequireAuthorization();
        group.MapPost("/password/change", ChangePasswordAsync).RequireAuthorization();
        group.MapGet("/sessions", GetSessionsAsync).RequireAuthorization();
        group.MapDelete("/sessions/{sessionId:guid}", RevokeSessionAsync).RequireAuthorization();
        group.MapPost("/sessions/revoke-others", RevokeOtherSessionsAsync).RequireAuthorization();
        group.MapGet("/security", GetSecurityAsync).RequireAuthorization();
        group.MapPost("/2fa/setup", SetupTwoFactorAsync).RequireAuthorization().RequireRateLimiting("auth-sensitive");
        group.MapPost("/2fa/enable", EnableTwoFactorAsync).RequireAuthorization().RequireRateLimiting("auth-sensitive");
        group.MapPost("/2fa/disable", DisableTwoFactorAsync).RequireAuthorization().RequireRateLimiting("auth-sensitive");
        group.MapPost("/2fa/recovery-codes/regenerate", RegenerateRecoveryCodesAsync).RequireAuthorization().RequireRateLimiting("auth-sensitive");
        group.MapPost("/email/verification", SendEmailVerificationAsync)
            .RequireAuthorization()
            .RequireRateLimiting("auth-resend-verification");
        group.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();

        return endpoints;
    }

    private static IResult GetExternalProvidersAsync(GoogleAuthenticationOptions googleOptions) =>
        Results.Ok(new ExternalAuthenticationProvidersResponse(googleOptions.Enabled,
            googleOptions.Enabled && !string.IsNullOrWhiteSpace(googleOptions.MobileCallbackUrl)));

    private static IResult StartGoogleAsync(string? client, GoogleAuthenticationOptions googleOptions)
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

    private static async Task<IResult> CompleteGoogleCallbackAsync(
        string? client,
        IGoogleExternalIdentityReader identityReader,
        GoogleAuthenticationService googleAuthentication,
        GoogleAuthenticationOptions googleOptions,
        HttpContext context,
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

        var identity = await identityReader.ReadAsync(context, cancellationToken);
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

    private static async Task<IResult> ExchangeGoogleCompletionAsync(
        GoogleCompletionRequest request,
        GoogleAuthenticationService googleAuthentication,
        GoogleAuthenticationOptions googleOptions,
        HttpContext context,
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
            context.Request.Headers.UserAgent.ToString(),
            cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> LinkGoogleAsync(
        GoogleLinkRequest request,
        GoogleAuthenticationService googleAuthentication,
        GoogleAuthenticationOptions googleOptions,
        HttpContext context,
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
            context.Request.Headers.UserAgent.ToString(),
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

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        RegistrationUseCase registrationUseCase,
        CancellationToken cancellationToken)
    {
        var result = await registrationUseCase.ExecuteAsync(request, cancellationToken);

        return result.Succeeded
            ? Results.Created("/api/auth/me", result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> StartRegistrationAsync(
        RegistrationStartRequest request,
        RegistrationChallengeService registrationChallengeService,
        CancellationToken cancellationToken)
    {
        var result = await registrationChallengeService.StartAsync(request, cancellationToken);
        return result.Succeeded
            ? Results.Accepted($"/api/auth/registration/{result.Value!.ChallengeId}", result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> ResendRegistrationAsync(
        RegistrationResendRequest request,
        RegistrationChallengeService registrationChallengeService,
        CancellationToken cancellationToken)
    {
        var result = await registrationChallengeService.ResendAsync(request, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> VerifyRegistrationAsync(
        RegistrationVerifyRequest request,
        RegistrationChallengeService registrationChallengeService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await registrationChallengeService.VerifyAsync(
            request,
            context.Request.Headers.UserAgent.ToString(),
            cancellationToken);
        return result.Succeeded
            ? Results.Created("/api/auth/me", result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        AuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.LoginAsync(request, null, cancellationToken);

        return result.Succeeded
            ? Results.Ok(result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> VerifyTwoFactorAsync(TwoFactorVerifyRequest request,
        AuthenticationService authenticationService, CancellationToken cancellationToken)
    {
        var result = await authenticationService.VerifyTwoFactorAsync(request, null, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        AuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.RefreshAsync(request, cancellationToken);

        return result.Succeeded
            ? Results.Ok(result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> RequestPasswordResetAsync(
        ForgotPasswordRequest request,
        AuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.RequestPasswordResetAsync(request, cancellationToken);

        return result.Succeeded
            ? Results.NoContent()
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        AuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.ResetPasswordAsync(request, cancellationToken);

        return result.Succeeded
            ? Results.NoContent()
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> VerifyEmailAsync(
        VerifyEmailRequest request,
        AuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.VerifyEmailAsync(request, cancellationToken);

        return result.Succeeded
            ? Results.NoContent()
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> LogoutAsync(
        LogoutRequest request,
        ClaimsPrincipal principal,
        AuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return InvalidAccessToken();
        }

        var result = await authenticationService.LogoutAsync(
            userId,
            request,
            cancellationToken);

        return result.Succeeded
            ? Results.NoContent()
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        ClaimsPrincipal principal,
        AuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return InvalidAccessToken();
        }

        var result = await authenticationService.ChangePasswordAsync(userId, request, cancellationToken);

        return result.Succeeded
            ? Results.Ok(result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetSessionsAsync(ClaimsPrincipal principal,
        AuthenticationService authenticationService, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId)) return InvalidAccessToken();
        Guid.TryParse(principal.FindFirstValue("sid"), out var sessionId);
        var result = await authenticationService.GetSessionsAsync(userId, sessionId == Guid.Empty ? null : sessionId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> RevokeSessionAsync(Guid sessionId, ClaimsPrincipal principal,
        AuthenticationService authenticationService, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId)) return InvalidAccessToken();
        var result = await authenticationService.RevokeSessionAsync(userId, sessionId, TimeProvider.System.GetUtcNow(), cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> RevokeOtherSessionsAsync(ClaimsPrincipal principal,
        AuthenticationService authenticationService, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId)) return InvalidAccessToken();
        Guid.TryParse(principal.FindFirstValue("sid"), out var currentSessionId);
        await authenticationService.RevokeOtherSessionsAsync(userId,
            currentSessionId == Guid.Empty ? null : currentSessionId, TimeProvider.System.GetUtcNow(), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetSecurityAsync(ClaimsPrincipal principal, AuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId)) return InvalidAccessToken();
        var result = await authenticationService.GetSecurityAsync(userId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> SetupTwoFactorAsync(ClaimsPrincipal principal, AuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId)) return InvalidAccessToken();
        var result = await authenticationService.SetupTwoFactorAsync(userId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> EnableTwoFactorAsync(TwoFactorCodeRequest request, ClaimsPrincipal principal,
        AuthenticationService authenticationService, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId)) return InvalidAccessToken();
        var result = await authenticationService.EnableTwoFactorAsync(userId, request.Code, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> RegenerateRecoveryCodesAsync(ClaimsPrincipal principal,
        AuthenticationService authenticationService, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId)) return InvalidAccessToken();
        var result = await authenticationService.RegenerateRecoveryCodesAsync(userId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> DisableTwoFactorAsync(DisableTwoFactorRequest request, ClaimsPrincipal principal,
        AuthenticationService authenticationService, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId)) return InvalidAccessToken();
        var result = await authenticationService.DisableTwoFactorAsync(userId, request.CurrentPassword, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> SendEmailVerificationAsync(
        ClaimsPrincipal principal,
        AuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return InvalidAccessToken();
        }

        var result = await authenticationService.SendEmailVerificationAsync(userId, cancellationToken);

        return result.Succeeded
            ? Results.NoContent()
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        AuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return InvalidAccessToken();
        }

        var result = await authenticationService.GetCurrentUserAsync(
            userId,
            cancellationToken);

        return result.Succeeded
            ? Results.Ok(result.Value)
            : result.Error!.ToHttpResult();
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);

    private static IResult InvalidAccessToken() =>
        new ApplicationError(
            ErrorCode.InvalidAccessToken,
            "The access token is invalid.",
            ApplicationErrorType.Unauthorized).ToHttpResult();
}
