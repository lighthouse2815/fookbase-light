using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Users.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UserRelationshipsController(FriendsService friendsService) : ControllerBase
{
    [HttpPost("{userId:guid}/follow")]
    public async Task<IResult> FollowAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await friendsService.FollowAsync(User.GetUserId(), userId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    [HttpDelete("{userId:guid}/follow")]
    public async Task<IResult> UnfollowAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await friendsService.UnfollowAsync(User.GetUserId(), userId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    [HttpGet("{userId:guid}/followers")]
    public async Task<IResult> GetFollowersAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = FriendsService.DefaultFollowPageSize)
    {
        var result = await friendsService.GetFollowersAsync(User.GetUserId(), userId, cursor, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("{userId:guid}/following")]
    public async Task<IResult> GetFollowingAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = FriendsService.DefaultFollowPageSize)
    {
        var result = await friendsService.GetFollowingAsync(User.GetUserId(), userId, cursor, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("{userId:guid}/friends")]
    public async Task<IResult> GetFriendsAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20)
    {
        var result = await friendsService.GetVisibleFriendsAsync(User.GetUserId(), userId, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }
}
