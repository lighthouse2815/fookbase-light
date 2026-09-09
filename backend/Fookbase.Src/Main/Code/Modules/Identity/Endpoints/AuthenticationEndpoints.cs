using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Application;

namespace Fookbase.Api.Modules.Identity.Endpoints;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth");

        group.MapPost("/register", RegisterAsync).AllowAnonymous();
        group.MapPost("/login", LoginAsync).AllowAnonymous();
        group.MapPost("/refresh", RefreshAsync).AllowAnonymous();
        group.MapPost("/logout", LogoutAsync).RequireAuthorization();
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
