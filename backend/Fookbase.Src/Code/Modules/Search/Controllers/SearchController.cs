using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Search.DTOs.Requests;
using Fookbase.Api.Modules.Search.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fookbase.Api.Modules.Search.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("search")]
[Route("api/search")]
public sealed class SearchController(SearchService searchService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] SearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await searchService.SearchAsync(
            actorUserId,
            request.Query,
            request.Type,
            request.Cursor,
            request.Limit,
            cancellationToken);
        return result.Succeeded
            ? Ok(result.Value)
            : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpGet("suggestions")]
    public async Task<IActionResult> SuggestionsAsync(
        [FromQuery] SearchSuggestionsRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await searchService.GetSuggestionsAsync(
            actorUserId, request.Query, request.Limit, cancellationToken);
        return result.Succeeded
            ? Ok(result.Value)
            : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    private static bool TryGetActorUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
