using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Feed.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Feed.Controllers;

[ApiController]
[Authorize]
[Route("api/feed")]
public sealed class FeedController(FeedService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> GetHomeFeedAsync(
        CancellationToken cancellationToken,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = FeedService.DefaultPageSize) =>
        GetFeedAsync(false, cursor, limit, cancellationToken);

    [HttpGet("following")]
    public Task<IActionResult> GetFollowingFeedAsync(
        CancellationToken cancellationToken,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = FeedService.DefaultPageSize) =>
        GetFeedAsync(true, cursor, limit, cancellationToken);

    private async Task<IActionResult> GetFeedAsync(
        bool following, string? cursor, int limit,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var viewerUserId))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(following
                ? await service.GetFollowingFeedAsync(viewerUserId, cursor, limit, cancellationToken)
                : await service.GetHomeFeedAsync(viewerUserId, cursor, limit, cancellationToken));
        }
        catch (FormatException)
        {
            return BadRequest(new
            {
                code = "invalid_feed_cursor",
                message = "The feed cursor or limit is invalid."
            });
        }
    }
}
