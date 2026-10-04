using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Events.DTOs.Requests;
using Fookbase.Api.Modules.Events.Services;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Events.Controllers;

[ApiController]
[Authorize]
[Route("api/events")]
public sealed class EventsController(EventsService service, MediaService mediaService) : ControllerBase
{
    [HttpPost]
    public Task<IActionResult> CreateAsync(CreateEventRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(actor => service.CreateAsync(actor, request, cancellationToken),
            value => Created($"/api/events/{value.Id}", value));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, GetActorUserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : Failure(result.Error!);
    }

    [HttpGet("{id:guid}/cover")]
    public async Task<IActionResult> GetCoverAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetCoverMediaIdAsync(id, GetActorUserId(), cancellationToken);
        if (!result.Succeeded)
        {
            return Failure(result.Error!);
        }

        var url = await mediaService.CreateReadUrlAsync(result.Value, cancellationToken);
        return url.Succeeded ? Redirect(url.Value!.Url) : NotFound();
    }

    [HttpPatch("{id:guid}")]
    public Task<IActionResult> UpdateAsync(Guid id, UpdateEventRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(actor => service.UpdateAsync(actor, id, request, cancellationToken), value => Ok(value));

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(actor => service.DeleteAsync(actor, id, cancellationToken));

    [HttpPost("{id:guid}/publish")]
    public Task<IActionResult> PublishAsync(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(actor => service.PublishAsync(actor, id, cancellationToken), value => Ok(value));

    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> CancelAsync(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(actor => service.CancelAsync(actor, id, cancellationToken), value => Ok(value));

    [HttpPost("{id:guid}/rsvp")]
    public Task<IActionResult> RsvpAsync(Guid id, SetEventRsvpRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(actor => service.RsvpAsync(actor, id, request.Status, cancellationToken), value => Ok(value));

    [HttpDelete("{id:guid}/rsvp")]
    public Task<IActionResult> RemoveRsvpAsync(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(actor => service.RemoveRsvpAsync(actor, id, cancellationToken));

    [HttpPost("{id:guid}/invites")]
    public Task<IActionResult> InviteAsync(Guid id, CreateEventInvitationRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(actor => service.InviteAsync(actor, id, request.UserId, cancellationToken),
            value => Created($"/api/events/invitations/{value.Id}", value));

    [HttpPost("invitations/{inviteId:guid}/accept")]
    public Task<IActionResult> AcceptInvitationAsync(Guid inviteId, CancellationToken cancellationToken) =>
        ExecuteAsync(actor => service.RespondInviteAsync(actor, inviteId, true, cancellationToken), value => Ok(value));

    [HttpPost("invitations/{inviteId:guid}/decline")]
    public Task<IActionResult> DeclineInvitationAsync(Guid inviteId, CancellationToken cancellationToken) =>
        ExecuteAsync(actor => service.RespondInviteAsync(actor, inviteId, false, cancellationToken), value => Ok(value));

    [HttpPost("{id:guid}/posts")]
    public Task<IActionResult> CreatePostAsync(Guid id, CreateEventPostRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(actor => service.CreatePostAsync(actor, id, request, cancellationToken),
            value => Created($"/api/posts/{value.Id}", value));

    [HttpGet("mine")]
    public Task<IActionResult> GetMineAsync(CancellationToken cancellationToken,
        string? cursor = null, int limit = EventsService.DefaultPageSize) =>
        ExecuteAsync(actor => service.GetMineAsync(actor, cursor, limit, cancellationToken), value => Ok(value));

    [HttpGet("upcoming")]
    public Task<IActionResult> GetUpcomingAsync(CancellationToken cancellationToken,
        string? cursor = null, int limit = EventsService.DefaultPageSize) =>
        ExecuteAsync(actor => service.UpcomingAsync(actor, cursor, limit, cancellationToken), value => Ok(value));

    [HttpGet("discover")]
    public Task<IActionResult> DiscoverAsync(CancellationToken cancellationToken,
        string? query = null, string? cursor = null, int limit = EventsService.DefaultPageSize) =>
        ExecuteAsync(actor => service.DiscoverAsync(actor, query, cursor, limit, cancellationToken), value => Ok(value));

    [HttpGet("invitations/mine")]
    public Task<IActionResult> GetInvitationsAsync(CancellationToken cancellationToken,
        string? cursor = null, int limit = EventsService.DefaultPageSize) =>
        ExecuteAsync(actor => service.InvitationsAsync(actor, cursor, limit, cancellationToken), value => Ok(value));

    [HttpGet("{id:guid}/participants")]
    public Task<IActionResult> GetParticipantsAsync(Guid id, CancellationToken cancellationToken,
        string? cursor = null, int limit = EventsService.DefaultPageSize) =>
        ExecuteAsync(actor => service.ParticipantsAsync(actor, id, cursor, limit, cancellationToken), value => Ok(value));

    [HttpGet("{id:guid}/posts")]
    public Task<IActionResult> GetPostsAsync(Guid id, CancellationToken cancellationToken,
        string? cursor = null, int limit = EventsService.DefaultPageSize) =>
        ExecuteAsync(actor => service.PostsAsync(actor, id, cursor, limit, cancellationToken), value => Ok(value));

    private Guid? GetActorUserId() =>
        Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    private async Task<IActionResult> ExecuteAsync<T>(Func<Guid, Task<ApplicationResult<T>>> action,
        Func<T, IActionResult> success)
    {
        var actor = GetActorUserId();
        if (actor is null)
        {
            return Unauthorized();
        }

        var result = await action(actor.Value);
        return result.Succeeded ? success(result.Value!) : Failure(result.Error!);
    }

    private async Task<IActionResult> ExecuteAsync(Func<Guid, Task<ApplicationResult>> action)
    {
        var actor = GetActorUserId();
        if (actor is null)
        {
            return Unauthorized();
        }

        var result = await action(actor.Value);
        return result.Succeeded ? NoContent() : Failure(result.Error!);
    }

    private static ObjectResult Failure(ApplicationError error) =>
        new(error.ToProblemDetails()) { StatusCode = error.ToStatusCode() };
}
