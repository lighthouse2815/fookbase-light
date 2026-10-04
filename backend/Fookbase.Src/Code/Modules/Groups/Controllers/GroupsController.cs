using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Groups.DTOs.Requests;
using Fookbase.Api.Modules.Groups.Services;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Posts.DTOs.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Groups.Controllers;

[ApiController]
[Authorize]
[Route("api/groups")]
public sealed class GroupsController(GroupsService service, MediaService mediaService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        CreateGroupRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await service.CreateAsync(actorUserId, request, cancellationToken);
        return result.Succeeded
            ? Created($"/api/groups/{result.Value!.Id}", result.Value)
            : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [AllowAnonymous]
    [HttpGet("{groupId:guid}")]
    public async Task<IActionResult> GetAsync(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(groupId, TryGetViewerUserId(User), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMineAsync(
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = GroupsService.DefaultPageSize)
    {
        if (!TryGetActorUserId(User, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await service.GetMineAsync(actorUserId, cursor, limit, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [AllowAnonymous]
    [HttpGet("discover")]
    public async Task<IActionResult> DiscoverAsync(
        CancellationToken cancellationToken,
        string? query = null,
        string? cursor = null,
        int limit = GroupsService.DefaultPageSize)
    {
        var result = await service.DiscoverAsync(query, cursor, limit, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpGet("feed")]
    public async Task<IActionResult> GetFeedAsync(
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = GroupsService.DefaultPageSize)
    {
        if (!TryGetActorUserId(User, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await service.GetFeedAsync(actorUserId, cursor, limit, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpGet("invites/mine")]
    public async Task<IActionResult> GetMyInvitesAsync(
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = GroupsService.DefaultPageSize)
    {
        if (!TryGetActorUserId(User, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await service.GetMyInvitesAsync(actorUserId, cursor, limit, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [AllowAnonymous]
    [HttpGet("{groupId:guid}/cover")]
    public async Task<IActionResult> GetCoverAsync(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var mediaId = await service.GetCoverMediaIdAsync(
            groupId,
            TryGetViewerUserId(User),
            cancellationToken);
        if (!mediaId.Succeeded)
        {
            return new ObjectResult(mediaId.Error!.ToProblemDetails()) { StatusCode = mediaId.Error!.ToStatusCode() };
        }

        var readUrl = await mediaService.CreateReadUrlAsync(mediaId.Value!, cancellationToken);
        return readUrl.Succeeded ? Redirect(readUrl.Value!.Url) : NotFound();
    }

    [HttpGet("{groupId:guid}/cover/access")]
    public async Task<IActionResult> GetCoverAccessAsync(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId))
        {
            return Unauthorized();
        }

        var mediaId = await service.GetCoverMediaIdAsync(groupId, actorUserId, cancellationToken);
        if (!mediaId.Succeeded)
        {
            return new ObjectResult(mediaId.Error!.ToProblemDetails()) { StatusCode = mediaId.Error!.ToStatusCode() };
        }

        var readUrl = await mediaService.CreateReadUrlAsync(mediaId.Value!, cancellationToken);
        return readUrl.Succeeded ? Ok(readUrl.Value) : NotFound();
    }

    [HttpPatch("{groupId:guid}")]
    public async Task<IActionResult> UpdateAsync(
        Guid groupId,
        UpdateGroupRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await service.UpdateAsync(actorUserId, groupId, request, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpDelete("{groupId:guid}")]
    public async Task<IActionResult> DeleteAsync(
        Guid groupId,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(actorUserId => service.DeleteAsync(actorUserId, groupId, cancellationToken));

    [HttpPost("{groupId:guid}/join")]
    public async Task<IActionResult> JoinAsync(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await service.JoinAsync(actorUserId, groupId, cancellationToken);
        if (!result.Succeeded)
        {
            return new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
        }

        return result.Value is null ? Ok() : Ok(result.Value);
    }

    [HttpPost("{groupId:guid}/leave")]
    public async Task<IActionResult> LeaveAsync(
        Guid groupId,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(actorUserId => service.LeaveAsync(actorUserId, groupId, cancellationToken));

    [AllowAnonymous]
    [HttpGet("{groupId:guid}/members")]
    public async Task<IActionResult> GetMembersAsync(
        Guid groupId,
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = GroupsService.DefaultPageSize)
    {
        var result = await service.GetMembersAsync(
            groupId,
            TryGetViewerUserId(User),
            cursor,
            limit,
            cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpPatch("{groupId:guid}/members/{userId:guid}/role")]
    public async Task<IActionResult> ChangeMemberRoleAsync(
        Guid groupId,
        Guid userId,
        ChangeGroupMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await service.ChangeMemberRoleAsync(
            actorUserId,
            groupId,
            userId,
            request,
            cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpDelete("{groupId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMemberAsync(
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(
            actorUserId => service.RemoveMemberAsync(actorUserId, groupId, userId, cancellationToken));

    [HttpGet("{groupId:guid}/join-requests")]
    public async Task<IActionResult> GetJoinRequestsAsync(
        Guid groupId,
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = GroupsService.DefaultPageSize)
    {
        if (!TryGetActorUserId(User, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await service.GetJoinRequestsAsync(
            actorUserId,
            groupId,
            cursor,
            limit,
            cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpPost("{groupId:guid}/join-requests/{requestId:guid}/approve")]
    public async Task<IActionResult> ApproveJoinRequestAsync(
        Guid groupId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await service.ApproveJoinRequestAsync(
            actorUserId,
            groupId,
            requestId,
            cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpPost("{groupId:guid}/join-requests/{requestId:guid}/decline")]
    public async Task<IActionResult> DeclineJoinRequestAsync(
        Guid groupId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await service.DeclineJoinRequestAsync(
            actorUserId,
            groupId,
            requestId,
            cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpPost("{groupId:guid}/invites")]
    public async Task<IActionResult> InviteAsync(
        Guid groupId,
        CreateGroupInviteRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await service.InviteAsync(actorUserId, groupId, request, cancellationToken);
        return result.Succeeded
            ? Created($"/api/groups/{groupId}/invites/{result.Value!.Id}", result.Value)
            : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpPost("{groupId:guid}/invites/{inviteId:guid}/accept")]
    public async Task<IActionResult> AcceptInviteAsync(
        Guid groupId,
        Guid inviteId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await service.AcceptInviteAsync(actorUserId, groupId, inviteId, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpPost("{groupId:guid}/invites/{inviteId:guid}/decline")]
    public async Task<IActionResult> DeclineInviteAsync(
        Guid groupId,
        Guid inviteId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await service.DeclineInviteAsync(actorUserId, groupId, inviteId, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [AllowAnonymous]
    [HttpGet("{groupId:guid}/rules")]
    public async Task<IActionResult> GetRulesAsync(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var result = await service.GetRulesAsync(groupId, TryGetViewerUserId(User), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpPost("{groupId:guid}/rules")]
    public async Task<IActionResult> CreateRuleAsync(
        Guid groupId,
        CreateGroupRuleRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await service.CreateRuleAsync(actorUserId, groupId, request, cancellationToken);
        return result.Succeeded
            ? Created($"/api/groups/{groupId}/rules/{result.Value!.Id}", result.Value)
            : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpPatch("{groupId:guid}/rules/{ruleId:guid}")]
    public async Task<IActionResult> UpdateRuleAsync(
        Guid groupId,
        Guid ruleId,
        UpdateGroupRuleRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await service.UpdateRuleAsync(actorUserId, groupId, ruleId, request, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpDelete("{groupId:guid}/rules/{ruleId:guid}")]
    public async Task<IActionResult> DeleteRuleAsync(
        Guid groupId,
        Guid ruleId,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(actorUserId => service.DeleteRuleAsync(
            actorUserId,
            groupId,
            ruleId,
            cancellationToken));

    [AllowAnonymous]
    [HttpGet("{groupId:guid}/posts")]
    public async Task<IActionResult> GetPostsAsync(
        Guid groupId,
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = GroupsService.DefaultPageSize)
    {
        var result = await service.GetPostsAsync(
            groupId,
            TryGetViewerUserId(User),
            cursor,
            limit,
            cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpPost("{groupId:guid}/posts")]
    public async Task<IActionResult> CreatePostAsync(
        Guid groupId,
        CreatePostRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await service.CreatePostAsync(actorUserId, groupId, request, cancellationToken);
        return result.Succeeded
            ? Created($"/api/posts/{result.Value!.Id}", result.Value)
            : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpDelete("{groupId:guid}/posts/{postId:guid}")]
    public async Task<IActionResult> RemovePostAsync(
        Guid groupId,
        Guid postId,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(actorUserId => service.RemovePostAsync(
            actorUserId,
            groupId,
            postId,
            cancellationToken));

    private async Task<IActionResult> ExecuteAsync(
        Func<Guid, Task<ApplicationResult>> command)
    {
        if (!TryGetActorUserId(User, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await command(actorUserId);
        return result.Succeeded ? NoContent() : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    private static bool TryGetActorUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);

    private static Guid? TryGetViewerUserId(ClaimsPrincipal principal) =>
        TryGetActorUserId(principal, out var userId) ? userId : null;
}
