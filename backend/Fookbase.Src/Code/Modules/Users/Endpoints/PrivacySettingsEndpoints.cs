using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Users.DTOs.Requests;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Users.Services;

namespace Fookbase.Api.Modules.Users.Endpoints;

public static class PrivacySettingsEndpoints
{
    public static IEndpointRouteBuilder MapPrivacySettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/privacy").RequireAuthorization();
        group.MapGet("", GetAsync);
        group.MapPatch("", UpdateAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(ClaimsPrincipal principal, UserPrivacySettingsService service,
        CancellationToken cancellationToken) =>
        TryGetUserId(principal, out var userId)
            ? ToHttpResult(await service.GetAsync(userId, cancellationToken))
            : Results.Unauthorized();

    private static async Task<IResult> UpdateAsync(UpdatePrivacySettingsRequest request, ClaimsPrincipal principal,
        UserPrivacySettingsService service, CancellationToken cancellationToken) =>
        TryGetUserId(principal, out var userId)
            ? ToHttpResult(await service.UpdateAsync(userId, request, cancellationToken))
            : Results.Unauthorized();

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);

    private static IResult ToHttpResult<T>(ApplicationResult<T> result) =>
        result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
}
