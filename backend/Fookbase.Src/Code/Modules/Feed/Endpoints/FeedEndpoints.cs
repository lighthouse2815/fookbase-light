using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Feed.Services;

namespace Fookbase.Api.Modules.Feed.Endpoints;

public static class FeedEndpoints
{
    public static IEndpointRouteBuilder MapFeedEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/feed", GetHomeFeedAsync).RequireAuthorization();
        endpoints.MapGet("/api/feed/following", GetFollowingFeedAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> GetHomeFeedAsync(
        ClaimsPrincipal principal,
        FeedService service,
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = FeedService.DefaultPageSize) =>
        await GetFeedAsync(principal, service, false, cursor, limit, cancellationToken);

    private static async Task<IResult> GetFollowingFeedAsync(
        ClaimsPrincipal principal,
        FeedService service,
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = FeedService.DefaultPageSize) =>
        await GetFeedAsync(principal, service, true, cursor, limit, cancellationToken);

    private static async Task<IResult> GetFeedAsync(
        ClaimsPrincipal principal, FeedService service, bool following, string? cursor, int limit,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var viewerUserId))
        {
            return Results.Unauthorized();
        }

        try
        {
            return Results.Ok(following
                ? await service.GetFollowingFeedAsync(viewerUserId, cursor, limit, cancellationToken)
                : await service.GetHomeFeedAsync(viewerUserId, cursor, limit, cancellationToken));
        }
        catch (FormatException)
        {
            return Results.BadRequest(new
            {
                code = "invalid_feed_cursor",
                message = "The feed cursor or limit is invalid."
            });
        }
    }
}
