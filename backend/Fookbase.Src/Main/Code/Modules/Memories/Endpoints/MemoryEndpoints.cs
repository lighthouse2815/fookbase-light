using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Memories.Services;

namespace Fookbase.Api.Modules.Memories.Endpoints;

public static class MemoryEndpoints
{
    public static IEndpointRouteBuilder MapMemoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/memories/today", GetTodayAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> GetTodayAsync(ClaimsPrincipal principal, MemoriesService service, CancellationToken cancellationToken) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)
            ? Results.Ok(await service.GetTodayAsync(userId, cancellationToken)) : Results.Unauthorized();
}
