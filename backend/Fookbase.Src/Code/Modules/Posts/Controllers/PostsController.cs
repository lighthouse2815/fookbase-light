using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Posts.DTOs.Requests;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Posts.Controllers;

[ApiController]
[Route("api/posts")]
public sealed class PostsController(PostsUseCase useCase) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<IResult> CreatePostAsync(
        [FromBody] CreatePostRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await useCase.CreatePostAsync(
            actorUserId, request.Content, request.Privacy, request.MediaIds ?? [], request.TextBackground, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/posts/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    [HttpPut("{postId:guid}")]
    [Authorize]
    public async Task<IResult> UpdatePostAsync(
        [FromRoute] Guid postId,
        [FromBody] UpdatePostRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await useCase.UpdatePostAsync(
            actorUserId, postId, request.Content, request.Privacy, request.MediaIds ?? [], cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpDelete("{postId:guid}")]
    [Authorize]
    public async Task<IResult> DeletePostAsync(
        [FromRoute] Guid postId,
        CancellationToken cancellationToken)
    {
        var result = await useCase.DeletePostAsync(User.GetUserId(), postId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    [HttpPut("{postId:guid}/pin")]
    [Authorize]
    public async Task<IResult> PinPostAsync(
        [FromRoute] Guid postId,
        CancellationToken cancellationToken)
    {
        var result = await useCase.SetPostPinnedAsync(User.GetUserId(), postId, true, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpDelete("{postId:guid}/pin")]
    [Authorize]
    public async Task<IResult> UnpinPostAsync(
        [FromRoute] Guid postId,
        CancellationToken cancellationToken)
    {
        var result = await useCase.SetPostPinnedAsync(User.GetUserId(), postId, false, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("{postId:guid}")]
    [AllowAnonymous]
    public async Task<IResult> GetPostAsync(
        [FromRoute] Guid postId,
        CancellationToken cancellationToken)
    {
        var viewerUserId = User.Identity?.IsAuthenticated == true ? User.GetUserId() : (Guid?)null;

        var result = await useCase.GetPostAsync(viewerUserId, postId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("feed")]
    [Authorize]
    public async Task<IResult> GetFeedAsync(
        CancellationToken cancellationToken,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20)
    {
        var actorUserId = User.GetUserId();

        var result = await useCase.GetFeedAsync(actorUserId, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("search")]
    [Authorize]
    public async Task<IResult> SearchPostsAsync(
        CancellationToken cancellationToken,
        [FromQuery] string? query = null,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20)
    {
        var actorUserId = User.GetUserId();

        var result = await useCase.SearchPostsAsync(actorUserId, query, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("users/{authorUserId:guid}")]
    [AllowAnonymous]
    public async Task<IResult> GetUserPostsAsync(
        [FromRoute] Guid authorUserId,
        CancellationToken cancellationToken,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20)
    {
        var viewerUserId = User.Identity?.IsAuthenticated == true ? User.GetUserId() : (Guid?)null;

        var result = await useCase.GetUserPostsAsync(
            viewerUserId, authorUserId, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpGet("{postId:guid}/media/{mediaId:guid}/access")]
    [Authorize]
    public async Task<IResult> GetMediaAccessAsync(
        [FromRoute] Guid postId,
        [FromRoute] Guid mediaId,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var result = await useCase.GetMediaAccessAsync(actorUserId, postId, mediaId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }
}
