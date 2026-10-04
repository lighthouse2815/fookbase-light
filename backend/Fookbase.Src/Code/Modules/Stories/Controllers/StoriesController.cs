using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Stories.DTOs.Requests;
using Fookbase.Api.Modules.Stories.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Stories.Controllers;

[ApiController]
[Authorize]
[Route("api/stories")]
public sealed class StoriesController(StoriesService service) : ControllerBase
{
    [HttpGet]
    public async Task<IResult> GetTrayAsync(
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Results.Unauthorized();
        var result = await service.GetActiveTrayAsync(actorUserId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPost]
    public async Task<IResult> CreateAsync(
        [FromBody] CreateStoryRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Results.Unauthorized();
        var result = await service.CreateAsync(
            actorUserId, request.MediaId, request.Caption, request.Privacy, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/stories/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    [HttpGet("archive")]
    public async Task<IResult> GetArchiveAsync(
        CancellationToken cancellationToken,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = StoriesService.DefaultPageSize)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Results.Unauthorized();
        var result = await service.GetArchiveAsync(actorUserId, cursor, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("{storyId:guid}")]
    public async Task<IResult> GetAsync(
        [FromRoute] Guid storyId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Results.Unauthorized();
        var result = await service.GetAsync(actorUserId, storyId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpDelete("{storyId:guid}")]
    public async Task<IResult> DeleteAsync(
        [FromRoute] Guid storyId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Results.Unauthorized();
        var result = await service.DeleteAsync(actorUserId, storyId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    [HttpPost("{storyId:guid}/view")]
    public async Task<IResult> RecordViewAsync(
        [FromRoute] Guid storyId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Results.Unauthorized();
        var result = await service.RecordViewAsync(actorUserId, storyId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    [HttpGet("{storyId:guid}/viewers")]
    public async Task<IResult> GetViewersAsync(
        [FromRoute] Guid storyId,
        CancellationToken cancellationToken,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = StoriesService.DefaultPageSize)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Results.Unauthorized();
        var result = await service.GetViewersAsync(actorUserId, storyId, cursor, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPost("{storyId:guid}/reaction")]
    public async Task<IResult> SetReactionAsync(
        [FromRoute] Guid storyId,
        [FromBody] SetStoryReactionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Results.Unauthorized();
        var result = await service.SetReactionAsync(actorUserId, storyId, request.Type, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpDelete("{storyId:guid}/reaction")]
    public async Task<IResult> RemoveReactionAsync(
        [FromRoute] Guid storyId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Results.Unauthorized();
        var result = await service.RemoveReactionAsync(actorUserId, storyId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    [HttpPost("{storyId:guid}/reply")]
    public async Task<IResult> ReplyAsync(
        [FromRoute] Guid storyId,
        [FromBody] CreateStoryReplyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Results.Unauthorized();
        var result = await service.ReplyAsync(actorUserId, storyId, request.Content, cancellationToken);
        return result.Succeeded ? Results.Created(
            $"/api/messages/conversations/{result.Value!.ConversationId}/messages/{result.Value.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    [HttpGet("{storyId:guid}/media/access")]
    public Task<IResult> GetVideoMediaAccessAsync(
        [FromRoute] Guid storyId,
        CancellationToken cancellationToken) =>
        GetMediaAccessAsync(storyId, false, cancellationToken);

    [HttpGet("{storyId:guid}/media/poster/access")]
    public Task<IResult> GetPosterMediaAccessAsync(
        [FromRoute] Guid storyId,
        CancellationToken cancellationToken) =>
        GetMediaAccessAsync(storyId, true, cancellationToken);

    private async Task<IResult> GetMediaAccessAsync(
        Guid storyId,
        bool poster,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Results.Unauthorized();
        var result = await service.GetMediaAccessAsync(actorUserId, storyId, poster, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static bool TryGetActorUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
