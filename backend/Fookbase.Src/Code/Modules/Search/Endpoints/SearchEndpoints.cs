using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Posts.Common;
using Fookbase.Api.Modules.Search.Services;

namespace Fookbase.Api.Modules.Search.Endpoints;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/search").RequireAuthorization().RequireRateLimiting("search");
        group.MapGet("", SearchAsync);
        group.MapGet("/suggestions", SuggestionsAsync);
        return endpoints;
    }

    private static async Task<IResult> SearchAsync(
        string? q,
        string? type,
        string? cursor,
        ClaimsPrincipal principal,
        SearchService searchService,
        CancellationToken cancellationToken,
        int limit = SearchService.DefaultPageSize)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await searchService.SearchAsync(
            actorUserId,
            q,
            type,
            cursor,
            limit,
            cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> SuggestionsAsync(
        string? q,
        ClaimsPrincipal principal,
        SearchService searchService,
        CancellationToken cancellationToken,
        int limit = SearchService.DefaultSuggestionLimit)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await searchService.GetSuggestionsAsync(actorUserId, q, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static bool TryGetActorUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
