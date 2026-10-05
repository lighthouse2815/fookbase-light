using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Friends.DTOs.Requests;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Identity.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Friends.Controllers;

[ApiController]
[Authorize]
[Route("api/friends")]
public sealed class FriendsController(FriendsService service, FriendSuggestionService suggestionService) : ControllerBase
{
    [HttpPost("requests/{userId:guid}")]
    public async Task<IResult> SendRequestAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await service.SendRequestAsync(actorUserId, userId, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/friends/requests/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    [HttpPost("requests/{requestId:guid}/accept")]
    public async Task<IResult> AcceptRequestAsync(
        [FromRoute] Guid requestId,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await service.AcceptRequestAsync(actorUserId, requestId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPost("requests/{requestId:guid}/decline")]
    public Task<IResult> DeclineRequestAsync(
        [FromRoute] Guid requestId,
        CancellationToken cancellationToken) =>
        ExecuteCommandAsync(
            actorUserId => service.DeclineRequestAsync(actorUserId, requestId, cancellationToken));

    [HttpDelete("requests/{requestId:guid}")]
    public Task<IResult> CancelRequestAsync(
        [FromRoute] Guid requestId,
        CancellationToken cancellationToken) =>
        ExecuteCommandAsync(
            actorUserId => service.CancelRequestAsync(actorUserId, requestId, cancellationToken));

    [HttpDelete("{userId:guid}")]
    public Task<IResult> UnfriendAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken) =>
        ExecuteCommandAsync(
            actorUserId => service.UnfriendAsync(actorUserId, userId, cancellationToken));

    [HttpPost("blocks/{userId:guid}")]
    public Task<IResult> BlockAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken) =>
        ExecuteCommandAsync(
            actorUserId => service.BlockAsync(actorUserId, userId, cancellationToken));

    [HttpDelete("blocks/{userId:guid}")]
    public Task<IResult> UnblockAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken) =>
        ExecuteCommandAsync(
            actorUserId => service.UnblockAsync(actorUserId, userId, cancellationToken));

    [HttpGet]
    public async Task<IResult> GetFriendsAsync(
        [FromQuery] FriendPageRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await service.GetFriendsAsync(actorUserId, request.Offset, request.Limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("requests/incoming")]
    public async Task<IResult> GetIncomingRequestsAsync(
        [FromQuery] FriendPageRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await service.GetIncomingRequestsAsync(actorUserId, request.Offset, request.Limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("requests/outgoing")]
    public async Task<IResult> GetOutgoingRequestsAsync(
        [FromQuery] FriendPageRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await service.GetOutgoingRequestsAsync(actorUserId, request.Offset, request.Limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("blocks")]
    public async Task<IResult> GetBlockedUsersAsync(
        [FromQuery] FriendPageRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await service.GetBlockedUsersAsync(actorUserId, request.Offset, request.Limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("notifications/unread")]
    public async Task<IResult> GetUnreadNotificationsAsync(
        [FromQuery] FriendPageRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await service.GetUnreadNotificationsAsync(actorUserId, request.Offset, request.Limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPost("notifications/{notificationId:guid}/read")]
    public Task<IResult> MarkNotificationReadAsync(
        [FromRoute] Guid notificationId,
        CancellationToken cancellationToken) =>
        ExecuteCommandAsync(
            actorUserId => service.MarkNotificationReadAsync(actorUserId, notificationId, cancellationToken));

    [HttpGet("status/{userId:guid}")]
    public async Task<IResult> GetStatusAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await service.GetStatusAsync(actorUserId, userId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("mutual/{userId:guid}")]
    public async Task<IResult> GetMutualFriendsAsync(
        [FromRoute] Guid userId,
        [FromQuery] FriendPageRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await service.GetMutualFriendsAsync(
            actorUserId,
            userId,
            request.Offset,
            request.Limit,
            cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("suggestions")]
    public async Task<IResult> GetSuggestionsAsync(
        [FromQuery] FriendSuggestionPageRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await suggestionService.GetSuggestionsAsync(actorUserId, request.Cursor, request.Limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private async Task<IResult> ExecuteCommandAsync(Func<Guid, Task<ApplicationResult>> command)
    {
        var result = await command(User.GetUserId());
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }
}
