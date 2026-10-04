using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Pages.DTOs.Requests;
using Fookbase.Api.Modules.Pages.Domain.Enums;
using Fookbase.Api.Modules.Pages.Services;
using Fookbase.Api.Modules.Posts.DTOs.Requests;
using Fookbase.Api.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Pages.Controllers;

[ApiController]
[Route("api/pages")]
public sealed class PagesController(PagesService service, MediaService mediaService) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreatePageRequest request,
        CancellationToken cancellationToken) => await ExecuteValueAsync(User,
        actorUserId => service.CreateAsync(actorUserId, request, cancellationToken), value => Created($"/api/pages/{value.Id}", value));

    [HttpGet("{idOrUsername}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAsync(
        [FromRoute] string idOrUsername,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(idOrUsername, ViewerId(User), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : Failure(result.Error!);
    }

    [HttpPatch("{pageId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateAsync(
        [FromRoute] Guid pageId,
        [FromBody] UpdatePageRequest request,
        CancellationToken cancellationToken) => await ExecuteValueAsync(User,
        actorUserId => service.UpdateAsync(actorUserId, pageId, request, cancellationToken), Ok);

    [HttpPatch("{pageId:guid}/media")]
    [Authorize]
    public async Task<IActionResult> SetMediaAsync(
        [FromRoute] Guid pageId,
        [FromBody] SetPageMediaRequest request,
        CancellationToken cancellationToken) => await ExecuteValueAsync(User,
        actorUserId => service.SetMediaAsync(actorUserId, pageId, request, cancellationToken), Ok);

    [HttpDelete("{pageId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteAsync(
        [FromRoute] Guid pageId,
        CancellationToken cancellationToken) => await ExecuteAsync(User, actorUserId => service.DeleteAsync(actorUserId, pageId, cancellationToken));

    [HttpPost("{pageId:guid}/publish")]
    [Authorize]
    public async Task<IActionResult> PublishAsync(
        [FromRoute] Guid pageId,
        CancellationToken cancellationToken) => await ExecuteValueAsync(User,
        actorUserId => service.PublishAsync(actorUserId, pageId, cancellationToken), Ok);

    [HttpPost("{pageId:guid}/unpublish")]
    [Authorize]
    public async Task<IActionResult> UnpublishAsync(
        [FromRoute] Guid pageId,
        CancellationToken cancellationToken) => await ExecuteValueAsync(User,
        actorUserId => service.UnpublishAsync(actorUserId, pageId, cancellationToken), Ok);

    [HttpPost("{pageId:guid}/follow")]
    [Authorize]
    public async Task<IActionResult> FollowAsync(
        [FromRoute] Guid pageId,
        CancellationToken cancellationToken) => await ExecuteAsync(User, actorUserId => service.FollowAsync(actorUserId, pageId, cancellationToken));

    [HttpDelete("{pageId:guid}/follow")]
    [Authorize]
    public async Task<IActionResult> UnfollowAsync(
        [FromRoute] Guid pageId,
        CancellationToken cancellationToken) => await ExecuteAsync(User, actorUserId => service.UnfollowAsync(actorUserId, pageId, cancellationToken));

    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> GetMineAsync(
        CancellationToken cancellationToken,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = PagesService.DefaultPageSize) => await ExecuteValueAsync(User,
        actorUserId => service.GetMineAsync(actorUserId, cursor, limit, cancellationToken), Ok);

    [HttpGet("following")]
    [Authorize]
    public async Task<IActionResult> GetFollowingAsync(
        CancellationToken cancellationToken,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = PagesService.DefaultPageSize) => await ExecuteValueAsync(User,
        actorUserId => service.GetFollowingAsync(actorUserId, cursor, limit, cancellationToken), Ok);

    [HttpGet("discover")]
    [AllowAnonymous]
    public async Task<IActionResult> DiscoverAsync(
        CancellationToken cancellationToken,
        [FromQuery] string? query = null,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = PagesService.DefaultPageSize)
    {
        var result = await service.DiscoverAsync(query, cursor, limit, ViewerId(User), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : Failure(result.Error!);
    }

    [HttpGet("{pageId:guid}/members")]
    [Authorize]
    public async Task<IActionResult> GetMembersAsync(
        [FromRoute] Guid pageId,
        CancellationToken cancellationToken,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = PagesService.DefaultPageSize) => await ExecuteValueAsync(User,
        actorUserId => service.GetMembersAsync(actorUserId, pageId, cursor, limit, cancellationToken), Ok);

    [HttpPost("{pageId:guid}/invitations")]
    [Authorize]
    public async Task<IActionResult> InviteAsync(
        [FromRoute] Guid pageId,
        [FromBody] CreatePageRoleInvitationRequest request,
        CancellationToken cancellationToken) => await ExecuteValueAsync(User,
        actorUserId => service.InviteAsync(actorUserId, pageId, request, cancellationToken),
        value => Created($"/api/pages/{pageId}/invitations/{value.Id}", value));

    [HttpGet("invitations/mine")]
    [Authorize]
    public async Task<IActionResult> GetMyInvitationsAsync(
        CancellationToken cancellationToken,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = PagesService.DefaultPageSize) => await ExecuteValueAsync(User,
        actorUserId => service.GetMyInvitationsAsync(actorUserId, cursor, limit, cancellationToken), Ok);

    [HttpPost("invitations/{inviteId:guid}/accept")]
    [Authorize]
    public async Task<IActionResult> AcceptInvitationAsync(
        [FromRoute] Guid inviteId,
        CancellationToken cancellationToken) => await ExecuteValueAsync(User,
        actorUserId => service.AcceptInvitationAsync(actorUserId, inviteId, cancellationToken), Ok);

    [HttpPost("invitations/{inviteId:guid}/decline")]
    [Authorize]
    public async Task<IActionResult> DeclineInvitationAsync(
        [FromRoute] Guid inviteId,
        CancellationToken cancellationToken) => await ExecuteValueAsync(User,
        actorUserId => service.DeclineInvitationAsync(actorUserId, inviteId, cancellationToken), Ok);

    [HttpPatch("{pageId:guid}/members/{userId:guid}/role")]
    [Authorize]
    public async Task<IActionResult> ChangeRoleAsync(
        [FromRoute] Guid pageId,
        [FromRoute] Guid userId,
        [FromBody] ChangePageMemberRoleRequest request,
        CancellationToken cancellationToken) => await ExecuteValueAsync(User,
        actorUserId => service.ChangeMemberRoleAsync(actorUserId, pageId, userId, request, cancellationToken), Ok);

    [HttpDelete("{pageId:guid}/members/{userId:guid}")]
    [Authorize]
    public async Task<IActionResult> RemoveMemberAsync(
        [FromRoute] Guid pageId,
        [FromRoute] Guid userId,
        CancellationToken cancellationToken) => await ExecuteAsync(User,
        actorUserId => service.RemoveMemberAsync(actorUserId, pageId, userId, cancellationToken));

    [HttpPost("{pageId:guid}/transfer-ownership")]
    [Authorize]
    public async Task<IActionResult> TransferOwnershipAsync(
        [FromRoute] Guid pageId,
        [FromBody] TransferPageOwnershipRequest request,
        CancellationToken cancellationToken) => await ExecuteAsync(User,
        actorUserId => service.TransferOwnershipAsync(actorUserId, pageId, request, cancellationToken));

    [HttpGet("{pageId:guid}/posts")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPostsAsync(
        [FromRoute] Guid pageId,
        CancellationToken cancellationToken,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = PagesService.DefaultPageSize)
    {
        var result = await service.GetPostsAsync(pageId, ViewerId(User), cursor, limit, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : Failure(result.Error!);
    }

    [HttpPost("{pageId:guid}/posts")]
    [Authorize]
    public async Task<IActionResult> CreatePostAsync(
        [FromRoute] Guid pageId,
        [FromBody] CreatePostRequest request,
        CancellationToken cancellationToken) => await ExecuteValueAsync(User,
        actorUserId => service.CreatePostAsync(actorUserId, pageId, request, cancellationToken),
        value => Created($"/api/posts/{value.Id}", value));

    [HttpGet("{pageId:guid}/avatar")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvatarAsync(
        [FromRoute] Guid pageId,
        CancellationToken cancellationToken) => await GetMediaAsync(pageId, PageMediaSlot.AVATAR, cancellationToken);

    [HttpGet("{pageId:guid}/cover")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCoverAsync(
        [FromRoute] Guid pageId,
        CancellationToken cancellationToken) => await GetMediaAsync(pageId, PageMediaSlot.COVER, cancellationToken);

    private async Task<IActionResult> GetMediaAsync(Guid pageId, PageMediaSlot slot, CancellationToken cancellationToken)
    {
        var mediaId = await service.GetMediaIdAsync(pageId, slot, ViewerId(User), cancellationToken);
        if (!mediaId.Succeeded) return Failure(mediaId.Error!);
        var readUrl = await mediaService.CreateReadUrlAsync(mediaId.Value!, cancellationToken);
        return readUrl.Succeeded ? Redirect(readUrl.Value!.Url) : NotFound();
    }

    private async Task<IActionResult> ExecuteAsync(ClaimsPrincipal principal, Func<Guid, Task<ApplicationResult>> command)
    {
        if (!ActorId(principal, out var actorUserId)) return Unauthorized();
        var result = await command(actorUserId);
        return result.Succeeded ? NoContent() : Failure(result.Error!);
    }

    private async Task<IActionResult> ExecuteValueAsync<T>(ClaimsPrincipal principal,
        Func<Guid, Task<ApplicationResult<T>>> command, Func<T, IActionResult> success)
    {
        if (!ActorId(principal, out var actorUserId)) return Unauthorized();
        var result = await command(actorUserId);
        return result.Succeeded ? success(result.Value!) : Failure(result.Error!);
    }

    private static ObjectResult Failure(ApplicationError error) =>
        new(error.ToProblemDetails()) { StatusCode = error.ToStatusCode() };

    private static bool ActorId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
    private static Guid? ViewerId(ClaimsPrincipal principal) => ActorId(principal, out var userId) ? userId : null;
}
