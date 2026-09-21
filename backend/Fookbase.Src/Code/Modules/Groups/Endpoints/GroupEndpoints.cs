using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Posts.Common;
using Fookbase.Api.Modules.Groups.DTOs.Requests;
using Fookbase.Api.Modules.Groups.Services;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Posts.DTOs.Requests;

namespace Fookbase.Api.Modules.Groups.Endpoints;

public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/groups");

        group.MapGet("/discover", DiscoverAsync).AllowAnonymous();
        group.MapGet("/mine", GetMineAsync).RequireAuthorization();
        group.MapGet("/feed", GetFeedAsync).RequireAuthorization();
        group.MapGet("/invites/mine", GetMyInvitesAsync).RequireAuthorization();
        group.MapPost("", CreateAsync).RequireAuthorization();
        group.MapGet("/{groupId:guid}", GetAsync).AllowAnonymous();
        group.MapGet("/{groupId:guid}/cover", GetCoverAsync).AllowAnonymous();
        group.MapPatch("/{groupId:guid}", UpdateAsync).RequireAuthorization();
        group.MapDelete("/{groupId:guid}", DeleteAsync).RequireAuthorization();

        group.MapPost("/{groupId:guid}/join", JoinAsync).RequireAuthorization();
        group.MapPost("/{groupId:guid}/leave", LeaveAsync).RequireAuthorization();
        group.MapGet("/{groupId:guid}/members", GetMembersAsync).AllowAnonymous();
        group.MapPatch("/{groupId:guid}/members/{userId:guid}/role", ChangeMemberRoleAsync)
            .RequireAuthorization();
        group.MapDelete("/{groupId:guid}/members/{userId:guid}", RemoveMemberAsync)
            .RequireAuthorization();

        group.MapGet("/{groupId:guid}/join-requests", GetJoinRequestsAsync).RequireAuthorization();
        group.MapPost("/{groupId:guid}/join-requests/{requestId:guid}/approve", ApproveJoinRequestAsync)
            .RequireAuthorization();
        group.MapPost("/{groupId:guid}/join-requests/{requestId:guid}/decline", DeclineJoinRequestAsync)
            .RequireAuthorization();

        group.MapPost("/{groupId:guid}/invites", InviteAsync).RequireAuthorization();
        group.MapPost("/{groupId:guid}/invites/{inviteId:guid}/accept", AcceptInviteAsync)
            .RequireAuthorization();
        group.MapPost("/{groupId:guid}/invites/{inviteId:guid}/decline", DeclineInviteAsync)
            .RequireAuthorization();

        group.MapGet("/{groupId:guid}/rules", GetRulesAsync).AllowAnonymous();
        group.MapPost("/{groupId:guid}/rules", CreateRuleAsync).RequireAuthorization();
        group.MapPatch("/{groupId:guid}/rules/{ruleId:guid}", UpdateRuleAsync).RequireAuthorization();
        group.MapDelete("/{groupId:guid}/rules/{ruleId:guid}", DeleteRuleAsync).RequireAuthorization();

        group.MapGet("/{groupId:guid}/posts", GetPostsAsync).AllowAnonymous();
        group.MapPost("/{groupId:guid}/posts", CreatePostAsync).RequireAuthorization();
        group.MapDelete("/{groupId:guid}/posts/{postId:guid}", RemovePostAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreateGroupRequest request,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.CreateAsync(actorUserId, request, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/groups/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetAsync(
        Guid groupId,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(groupId, TryGetViewerUserId(principal), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetMineAsync(
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = GroupsService.DefaultPageSize)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.GetMineAsync(actorUserId, cursor, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> DiscoverAsync(
        GroupsService service,
        CancellationToken cancellationToken,
        string? query = null,
        string? cursor = null,
        int limit = GroupsService.DefaultPageSize)
    {
        var result = await service.DiscoverAsync(query, cursor, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetFeedAsync(
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = GroupsService.DefaultPageSize)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.GetFeedAsync(actorUserId, cursor, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetMyInvitesAsync(
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = GroupsService.DefaultPageSize)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.GetMyInvitesAsync(actorUserId, cursor, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetCoverAsync(
        Guid groupId,
        ClaimsPrincipal principal,
        GroupsService service,
        MediaService mediaService,
        CancellationToken cancellationToken)
    {
        var mediaId = await service.GetCoverMediaIdAsync(
            groupId,
            TryGetViewerUserId(principal),
            cancellationToken);
        if (!mediaId.Succeeded)
        {
            return mediaId.Error!.ToHttpResult();
        }

        var readUrl = await mediaService.CreateReadUrlAsync(mediaId.Value!, cancellationToken);
        return readUrl.Succeeded ? Results.Redirect(readUrl.Value!.Url) : Results.NotFound();
    }

    private static async Task<IResult> UpdateAsync(
        Guid groupId,
        UpdateGroupRequest request,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.UpdateAsync(actorUserId, groupId, request, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> DeleteAsync(
        Guid groupId,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(principal, actorUserId => service.DeleteAsync(actorUserId, groupId, cancellationToken));

    private static async Task<IResult> JoinAsync(
        Guid groupId,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.JoinAsync(actorUserId, groupId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> LeaveAsync(
        Guid groupId,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(principal, actorUserId => service.LeaveAsync(actorUserId, groupId, cancellationToken));

    private static async Task<IResult> GetMembersAsync(
        Guid groupId,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = GroupsService.DefaultPageSize)
    {
        var result = await service.GetMembersAsync(
            groupId,
            TryGetViewerUserId(principal),
            cursor,
            limit,
            cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> ChangeMemberRoleAsync(
        Guid groupId,
        Guid userId,
        ChangeGroupMemberRoleRequest request,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.ChangeMemberRoleAsync(
            actorUserId,
            groupId,
            userId,
            request,
            cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> RemoveMemberAsync(
        Guid groupId,
        Guid userId,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(
            principal,
            actorUserId => service.RemoveMemberAsync(actorUserId, groupId, userId, cancellationToken));

    private static async Task<IResult> GetJoinRequestsAsync(
        Guid groupId,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = GroupsService.DefaultPageSize)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.GetJoinRequestsAsync(
            actorUserId,
            groupId,
            cursor,
            limit,
            cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> ApproveJoinRequestAsync(
        Guid groupId,
        Guid requestId,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.ApproveJoinRequestAsync(
            actorUserId,
            groupId,
            requestId,
            cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> DeclineJoinRequestAsync(
        Guid groupId,
        Guid requestId,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.DeclineJoinRequestAsync(
            actorUserId,
            groupId,
            requestId,
            cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> InviteAsync(
        Guid groupId,
        CreateGroupInviteRequest request,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.InviteAsync(actorUserId, groupId, request, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/groups/{groupId}/invites/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> AcceptInviteAsync(
        Guid groupId,
        Guid inviteId,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.AcceptInviteAsync(actorUserId, groupId, inviteId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> DeclineInviteAsync(
        Guid groupId,
        Guid inviteId,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.DeclineInviteAsync(actorUserId, groupId, inviteId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetRulesAsync(
        Guid groupId,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetRulesAsync(groupId, TryGetViewerUserId(principal), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> CreateRuleAsync(
        Guid groupId,
        CreateGroupRuleRequest request,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.CreateRuleAsync(actorUserId, groupId, request, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/groups/{groupId}/rules/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> UpdateRuleAsync(
        Guid groupId,
        Guid ruleId,
        UpdateGroupRuleRequest request,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.UpdateRuleAsync(actorUserId, groupId, ruleId, request, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> DeleteRuleAsync(
        Guid groupId,
        Guid ruleId,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(principal, actorUserId => service.DeleteRuleAsync(
            actorUserId,
            groupId,
            ruleId,
            cancellationToken));

    private static async Task<IResult> GetPostsAsync(
        Guid groupId,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = GroupsService.DefaultPageSize)
    {
        var result = await service.GetPostsAsync(
            groupId,
            TryGetViewerUserId(principal),
            cursor,
            limit,
            cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> CreatePostAsync(
        Guid groupId,
        CreatePostRequest request,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.CreatePostAsync(actorUserId, groupId, request, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/posts/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> RemovePostAsync(
        Guid groupId,
        Guid postId,
        ClaimsPrincipal principal,
        GroupsService service,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(principal, actorUserId => service.RemovePostAsync(
            actorUserId,
            groupId,
            postId,
            cancellationToken));

    private static async Task<IResult> ExecuteAsync(
        ClaimsPrincipal principal,
        Func<Guid, Task<ApplicationResult>> command)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await command(actorUserId);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static bool TryGetActorUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);

    private static Guid? TryGetViewerUserId(ClaimsPrincipal principal) =>
        TryGetActorUserId(principal, out var userId) ? userId : null;
}
