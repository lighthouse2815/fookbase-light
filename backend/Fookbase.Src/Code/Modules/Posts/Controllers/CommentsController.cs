using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Posts.DTOs.Requests;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Posts.Controllers;

[ApiController]
[Route("api/posts")]
public sealed class CommentsController(PostsUseCase useCase) : ControllerBase
{
    [HttpPost("{postId:guid}/comments")]
    [Authorize]
    public async Task<IResult> CreateCommentAsync(
        [FromRoute] Guid postId,
        [FromBody] CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await useCase.CreateCommentAsync(
            actorUserId, postId, request.ParentCommentId, request.Content, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/posts/comments/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    [HttpPut("comments/{commentId:guid}")]
    [Authorize]
    public async Task<IResult> UpdateCommentAsync(
        [FromRoute] Guid commentId,
        [FromBody] UpdateCommentRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await useCase.UpdateCommentAsync(
            actorUserId, commentId, request.Content, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpDelete("comments/{commentId:guid}")]
    [Authorize]
    public async Task<IResult> DeleteCommentAsync(
        [FromRoute] Guid commentId,
        CancellationToken cancellationToken)
    {
        var result = await useCase.DeleteCommentAsync(User.GetUserId(), commentId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    [HttpGet("{postId:guid}/comments")]
    [AllowAnonymous]
    public async Task<IResult> GetCommentsAsync(
        [FromRoute] Guid postId,
        CancellationToken cancellationToken,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20)
    {
        var viewerUserId = User.Identity?.IsAuthenticated == true ? User.GetUserId() : (Guid?)null;

        var result = await useCase.GetCommentsAsync(
            viewerUserId, postId, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPut("comments/{commentId:guid}/reaction")]
    [Authorize]
    public async Task<IResult> SetCommentReactionAsync(
        [FromRoute] Guid commentId,
        [FromBody] SetReactionRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await useCase.SetCommentReactionAsync(
            actorUserId, commentId, request.Type, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpDelete("comments/{commentId:guid}/reaction")]
    [Authorize]
    public async Task<IResult> RemoveCommentReactionAsync(
        [FromRoute] Guid commentId,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await useCase.RemoveCommentReactionAsync(actorUserId, commentId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }
}
