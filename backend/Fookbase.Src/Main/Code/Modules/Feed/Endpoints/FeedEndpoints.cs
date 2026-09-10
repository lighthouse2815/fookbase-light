using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Feed.Services;

namespace Fookbase.Api.Modules.Feed.Endpoints;

public static class FeedEndpoints
{
    public static IEndpointRouteBuilder MapFeedEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/feed", GetHomeFeedAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> GetHomeFeedAsync(
        ClaimsPrincipal principal,
        FeedService service,
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = FeedService.DefaultPageSize)
    {
        if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var viewerUserId))
        {
            return Results.Unauthorized();
        }

        if (limit is < 1 or > FeedService.MaximumPageSize || !FeedService.IsValidCursor(cursor))
        {
            return Results.BadRequest(new
            {
                code = "invalid_feed_cursor",
                message = "The feed cursor or limit is invalid."
            });
        }

        return Results.Ok(await service.GetHomeFeedAsync(
            viewerUserId,
            cursor,
            limit,
            cancellationToken));
    }
}
