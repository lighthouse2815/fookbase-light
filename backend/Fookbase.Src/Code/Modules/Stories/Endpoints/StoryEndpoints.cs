using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Posts.Common;
using Fookbase.Api.Modules.Stories.DTOs.Requests;
using Fookbase.Api.Modules.Stories.Services;

namespace Fookbase.Api.Modules.Stories.Endpoints;

public static class StoryEndpoints
{
    public static IEndpointRouteBuilder MapStoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/stories").RequireAuthorization();
        group.MapGet("", GetTrayAsync);
        group.MapPost("", CreateAsync);
        group.MapGet("/archive", GetArchiveAsync);
        group.MapGet("/{storyId:guid}", GetAsync);
        group.MapDelete("/{storyId:guid}", DeleteAsync);
        group.MapPost("/{storyId:guid}/view", RecordViewAsync);
        group.MapGet("/{storyId:guid}/viewers", GetViewersAsync);
        group.MapPost("/{storyId:guid}/reaction", SetReactionAsync);
        group.MapDelete("/{storyId:guid}/reaction", RemoveReactionAsync);
        group.MapPost("/{storyId:guid}/reply", ReplyAsync);
        group.MapGet("/{storyId:guid}/media/access", GetVideoMediaAccessAsync);
        group.MapGet("/{storyId:guid}/media/poster/access", GetPosterMediaAccessAsync);
        return endpoints;
    }

    private static async Task<IResult> GetTrayAsync(
        ClaimsPrincipal principal,
        StoriesService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.GetActiveTrayAsync(actorUserId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> CreateAsync(
        CreateStoryRequest request,
        ClaimsPrincipal principal,
        StoriesService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.CreateAsync(
            actorUserId, request.MediaId, request.Caption, request.Privacy, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/stories/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetArchiveAsync(
        ClaimsPrincipal principal,
        StoriesService service,
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = StoriesService.DefaultPageSize)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.GetArchiveAsync(actorUserId, cursor, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetAsync(
        Guid storyId,
        ClaimsPrincipal principal,
        StoriesService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.GetAsync(actorUserId, storyId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> DeleteAsync(
        Guid storyId,
        ClaimsPrincipal principal,
        StoriesService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.DeleteAsync(actorUserId, storyId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> RecordViewAsync(
        Guid storyId,
        ClaimsPrincipal principal,
        StoriesService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.RecordViewAsync(actorUserId, storyId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetViewersAsync(
        Guid storyId,
        ClaimsPrincipal principal,
        StoriesService service,
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = StoriesService.DefaultPageSize)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.GetViewersAsync(actorUserId, storyId, cursor, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> SetReactionAsync(
        Guid storyId,
        SetStoryReactionRequest request,
        ClaimsPrincipal principal,
        StoriesService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.SetReactionAsync(actorUserId, storyId, request.Type, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> RemoveReactionAsync(
        Guid storyId,
        ClaimsPrincipal principal,
        StoriesService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.RemoveReactionAsync(actorUserId, storyId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> ReplyAsync(
        Guid storyId,
        CreateStoryReplyRequest request,
        ClaimsPrincipal principal,
        StoriesService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.ReplyAsync(actorUserId, storyId, request.Content, cancellationToken);
        return result.Succeeded ? Results.Created(
            $"/api/messages/conversations/{result.Value!.ConversationId}/messages/{result.Value.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    private static Task<IResult> GetVideoMediaAccessAsync(
        Guid storyId,
        ClaimsPrincipal principal,
        StoriesService service,
        CancellationToken cancellationToken) =>
        GetMediaAccessAsync(storyId, false, principal, service, cancellationToken);

    private static Task<IResult> GetPosterMediaAccessAsync(
        Guid storyId,
        ClaimsPrincipal principal,
        StoriesService service,
        CancellationToken cancellationToken) =>
        GetMediaAccessAsync(storyId, true, principal, service, cancellationToken);

    private static async Task<IResult> GetMediaAccessAsync(
        Guid storyId,
        bool poster,
        ClaimsPrincipal principal,
        StoriesService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.GetMediaAccessAsync(actorUserId, storyId, poster, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static bool TryGetActorUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
