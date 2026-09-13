using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace Fookbase.Api.Modules.Identity.Endpoints;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth");

        group.MapPost("/register", RegisterAsync).AllowAnonymous();
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
            "invalid_access_token",
            "The access token is invalid.",
            ApplicationErrorType.Unauthorized).ToHttpResult();
}
