using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Users.Services.Profiles;

namespace Fookbase.Api.Modules.Users.Endpoints;

public static class UserProfileEndpoints
{
    public static IEndpointRouteBuilder MapUserProfileEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/users");

        group.MapGet("/{userId:guid}", GetByIdAsync).AllowAnonymous();
        group.MapGet("/me", GetCurrentAsync).RequireAuthorization();
        group.MapPatch("/me", UpdateCurrentAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> GetByIdAsync(
        Guid userId,
        IUserProfileService profileService,
        CancellationToken cancellationToken)
    {
        var result = await profileService.GetAsync(userId, cancellationToken);
        return result.Succeeded
            ? Results.Ok(result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetCurrentAsync(
        ClaimsPrincipal principal,
        IUserProfileService profileService,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await profileService.GetAsync(userId, cancellationToken);
        return result.Succeeded
            ? Results.Ok(result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> UpdateCurrentAsync(
        UpdateUserProfileRequest request,
        ClaimsPrincipal principal,
        IUserProfileService profileService,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await profileService.UpdateAsync(userId, request, cancellationToken);
        return result.Succeeded
            ? Results.Ok(result.Value)
            : result.Error!.ToHttpResult();
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
