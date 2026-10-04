using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Pages.DTOs.Requests;
using Fookbase.Api.Modules.Pages.Entities;
using Fookbase.Api.Modules.Pages.Services;
using Fookbase.Api.Modules.Posts.DTOs.Requests;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Pages.Endpoints;

public static class PageEndpoints
{
    public static IEndpointRouteBuilder MapPageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var pages = endpoints.MapGroup("/api/pages");
        pages.MapGet("/discover", DiscoverAsync).AllowAnonymous();
        pages.MapGet("/mine", GetMineAsync).RequireAuthorization();
        pages.MapGet("/following", GetFollowingAsync).RequireAuthorization();
        pages.MapGet("/invitations/mine", GetMyInvitationsAsync).RequireAuthorization();
        pages.MapPost("/invitations/{inviteId:guid}/accept", AcceptInvitationAsync).RequireAuthorization();
        pages.MapPost("/invitations/{inviteId:guid}/decline", DeclineInvitationAsync).RequireAuthorization();
        pages.MapPost("", CreateAsync).RequireAuthorization();
        pages.MapGet("/{idOrUsername}", GetAsync).AllowAnonymous();
        pages.MapPatch("/{pageId:guid}", UpdateAsync).RequireAuthorization();
        pages.MapPatch("/{pageId:guid}/media", SetMediaAsync).RequireAuthorization();
        pages.MapDelete("/{pageId:guid}", DeleteAsync).RequireAuthorization();
        pages.MapPost("/{pageId:guid}/publish", PublishAsync).RequireAuthorization();
        pages.MapPost("/{pageId:guid}/unpublish", UnpublishAsync).RequireAuthorization();
        pages.MapGet("/{pageId:guid}/avatar", GetAvatarAsync).AllowAnonymous();
        pages.MapGet("/{pageId:guid}/cover", GetCoverAsync).AllowAnonymous();
        pages.MapPost("/{pageId:guid}/follow", FollowAsync).RequireAuthorization();
        pages.MapDelete("/{pageId:guid}/follow", UnfollowAsync).RequireAuthorization();
        pages.MapGet("/{pageId:guid}/members", GetMembersAsync).RequireAuthorization();
        pages.MapPost("/{pageId:guid}/invitations", InviteAsync).RequireAuthorization();
        pages.MapPatch("/{pageId:guid}/members/{userId:guid}/role", ChangeRoleAsync).RequireAuthorization();
        pages.MapDelete("/{pageId:guid}/members/{userId:guid}", RemoveMemberAsync).RequireAuthorization();
        pages.MapPost("/{pageId:guid}/transfer-ownership", TransferOwnershipAsync).RequireAuthorization();
        pages.MapGet("/{pageId:guid}/posts", GetPostsAsync).AllowAnonymous();
        pages.MapPost("/{pageId:guid}/posts", CreatePostAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreatePageRequest request, ClaimsPrincipal principal, PagesService service,
        CancellationToken cancellationToken) => await ExecuteValueAsync(principal,
        actorUserId => service.CreateAsync(actorUserId, request, cancellationToken), value => Results.Created($"/api/pages/{value.Id}", value));

    private static async Task<IResult> GetAsync(string idOrUsername, ClaimsPrincipal principal, PagesService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(idOrUsername, ViewerId(principal), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> UpdateAsync(Guid pageId, UpdatePageRequest request, ClaimsPrincipal principal,
        PagesService service, CancellationToken cancellationToken) => await ExecuteValueAsync(principal,
        actorUserId => service.UpdateAsync(actorUserId, pageId, request, cancellationToken), Results.Ok);

    private static async Task<IResult> SetMediaAsync(Guid pageId, SetPageMediaRequest request, ClaimsPrincipal principal,
        PagesService service, CancellationToken cancellationToken) => await ExecuteValueAsync(principal,
        actorUserId => service.SetMediaAsync(actorUserId, pageId, request, cancellationToken), Results.Ok);

    private static async Task<IResult> DeleteAsync(Guid pageId, ClaimsPrincipal principal, PagesService service,
        CancellationToken cancellationToken) => await ExecuteAsync(principal, actorUserId => service.DeleteAsync(actorUserId, pageId, cancellationToken));

    private static async Task<IResult> PublishAsync(Guid pageId, ClaimsPrincipal principal, PagesService service,
        CancellationToken cancellationToken) => await ExecuteValueAsync(principal,
        actorUserId => service.PublishAsync(actorUserId, pageId, cancellationToken), Results.Ok);

    private static async Task<IResult> UnpublishAsync(Guid pageId, ClaimsPrincipal principal, PagesService service,
        CancellationToken cancellationToken) => await ExecuteValueAsync(principal,
        actorUserId => service.UnpublishAsync(actorUserId, pageId, cancellationToken), Results.Ok);

    private static async Task<IResult> FollowAsync(Guid pageId, ClaimsPrincipal principal, PagesService service,
        CancellationToken cancellationToken) => await ExecuteAsync(principal, actorUserId => service.FollowAsync(actorUserId, pageId, cancellationToken));

    private static async Task<IResult> UnfollowAsync(Guid pageId, ClaimsPrincipal principal, PagesService service,
        CancellationToken cancellationToken) => await ExecuteAsync(principal, actorUserId => service.UnfollowAsync(actorUserId, pageId, cancellationToken));

    private static async Task<IResult> GetMineAsync(ClaimsPrincipal principal, PagesService service, CancellationToken cancellationToken,
        string? cursor = null, int limit = PagesService.DefaultPageSize) => await ExecuteValueAsync(principal,
        actorUserId => service.GetMineAsync(actorUserId, cursor, limit, cancellationToken), Results.Ok);

    private static async Task<IResult> GetFollowingAsync(ClaimsPrincipal principal, PagesService service, CancellationToken cancellationToken,
        string? cursor = null, int limit = PagesService.DefaultPageSize) => await ExecuteValueAsync(principal,
        actorUserId => service.GetFollowingAsync(actorUserId, cursor, limit, cancellationToken), Results.Ok);

    private static async Task<IResult> DiscoverAsync(ClaimsPrincipal principal, PagesService service, CancellationToken cancellationToken,
        string? query = null, string? cursor = null, int limit = PagesService.DefaultPageSize)
    {
        var result = await service.DiscoverAsync(query, cursor, limit, ViewerId(principal), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetMembersAsync(Guid pageId, ClaimsPrincipal principal, PagesService service,
        CancellationToken cancellationToken, string? cursor = null, int limit = PagesService.DefaultPageSize) => await ExecuteValueAsync(principal,
        actorUserId => service.GetMembersAsync(actorUserId, pageId, cursor, limit, cancellationToken), Results.Ok);

    private static async Task<IResult> InviteAsync(Guid pageId, CreatePageRoleInvitationRequest request, ClaimsPrincipal principal,
        PagesService service, CancellationToken cancellationToken) => await ExecuteValueAsync(principal,
        actorUserId => service.InviteAsync(actorUserId, pageId, request, cancellationToken),
        value => Results.Created($"/api/pages/{pageId}/invitations/{value.Id}", value));

    private static async Task<IResult> GetMyInvitationsAsync(ClaimsPrincipal principal, PagesService service,
        CancellationToken cancellationToken, string? cursor = null, int limit = PagesService.DefaultPageSize) => await ExecuteValueAsync(principal,
        actorUserId => service.GetMyInvitationsAsync(actorUserId, cursor, limit, cancellationToken), Results.Ok);

    private static async Task<IResult> AcceptInvitationAsync(Guid inviteId, ClaimsPrincipal principal, PagesService service,
        CancellationToken cancellationToken) => await ExecuteValueAsync(principal,
        actorUserId => service.AcceptInvitationAsync(actorUserId, inviteId, cancellationToken), Results.Ok);

    private static async Task<IResult> DeclineInvitationAsync(Guid inviteId, ClaimsPrincipal principal, PagesService service,
        CancellationToken cancellationToken) => await ExecuteValueAsync(principal,
        actorUserId => service.DeclineInvitationAsync(actorUserId, inviteId, cancellationToken), Results.Ok);

    private static async Task<IResult> ChangeRoleAsync(Guid pageId, Guid userId, ChangePageMemberRoleRequest request,
        ClaimsPrincipal principal, PagesService service, CancellationToken cancellationToken) => await ExecuteValueAsync(principal,
        actorUserId => service.ChangeMemberRoleAsync(actorUserId, pageId, userId, request, cancellationToken), Results.Ok);

    private static async Task<IResult> RemoveMemberAsync(Guid pageId, Guid userId, ClaimsPrincipal principal, PagesService service,
        CancellationToken cancellationToken) => await ExecuteAsync(principal,
        actorUserId => service.RemoveMemberAsync(actorUserId, pageId, userId, cancellationToken));

    private static async Task<IResult> TransferOwnershipAsync(Guid pageId, TransferPageOwnershipRequest request, ClaimsPrincipal principal,
        PagesService service, CancellationToken cancellationToken) => await ExecuteAsync(principal,
        actorUserId => service.TransferOwnershipAsync(actorUserId, pageId, request, cancellationToken));

    private static async Task<IResult> GetPostsAsync(Guid pageId, ClaimsPrincipal principal, PagesService service,
        CancellationToken cancellationToken, string? cursor = null, int limit = PagesService.DefaultPageSize)
    {
        var result = await service.GetPostsAsync(pageId, ViewerId(principal), cursor, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> CreatePostAsync(Guid pageId, CreatePostRequest request, ClaimsPrincipal principal,
        PagesService service, CancellationToken cancellationToken) => await ExecuteValueAsync(principal,
        actorUserId => service.CreatePostAsync(actorUserId, pageId, request, cancellationToken),
        value => Results.Created($"/api/posts/{value.Id}", value));

    private static async Task<IResult> GetAvatarAsync(Guid pageId, ClaimsPrincipal principal, PagesService service, MediaService mediaService,
        CancellationToken cancellationToken) => await GetMediaAsync(pageId, PageMediaSlot.AVATAR, principal, service, mediaService, cancellationToken);

    private static async Task<IResult> GetCoverAsync(Guid pageId, ClaimsPrincipal principal, PagesService service, MediaService mediaService,
        CancellationToken cancellationToken) => await GetMediaAsync(pageId, PageMediaSlot.COVER, principal, service, mediaService, cancellationToken);

    private static async Task<IResult> GetMediaAsync(Guid pageId, PageMediaSlot slot, ClaimsPrincipal principal, PagesService service,
        MediaService mediaService, CancellationToken cancellationToken)
    {
        var mediaId = await service.GetMediaIdAsync(pageId, slot, ViewerId(principal), cancellationToken);
        if (!mediaId.Succeeded) return mediaId.Error!.ToHttpResult();
        var readUrl = await mediaService.CreateReadUrlAsync(mediaId.Value!, cancellationToken);
        return readUrl.Succeeded ? Results.Redirect(readUrl.Value!.Url) : Results.NotFound();
    }

    private static async Task<IResult> ExecuteAsync(ClaimsPrincipal principal, Func<Guid, Task<ApplicationResult>> command)
    {
        if (!ActorId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await command(actorUserId);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> ExecuteValueAsync<T>(ClaimsPrincipal principal,
        Func<Guid, Task<ApplicationResult<T>>> command, Func<T, IResult> success)
    {
        if (!ActorId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await command(actorUserId);
        return result.Succeeded ? success(result.Value!) : result.Error!.ToHttpResult();
    }

    private static bool ActorId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
    private static Guid? ViewerId(ClaimsPrincipal principal) => ActorId(principal, out var userId) ? userId : null;
}
