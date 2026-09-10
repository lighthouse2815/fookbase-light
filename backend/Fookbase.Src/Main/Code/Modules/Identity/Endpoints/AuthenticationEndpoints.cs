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
        var result = await authenticationService.LoginAsync(request, cancellationToken);

        return result.Succeeded
            ? Results.Ok(result.Value)
            : result.Error!.ToHttpResult();
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
