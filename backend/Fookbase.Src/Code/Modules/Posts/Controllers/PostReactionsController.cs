using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Posts.DTOs.Requests;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Posts.Controllers;

[ApiController]
[Authorize]
[Route("api/posts")]
public sealed class PostReactionsController(PostsUseCase useCase) : ControllerBase
{
    [HttpGet("{postId:guid}/reactions")]
    public async Task<IResult> GetReactionsAsync(
        [FromRoute] Guid postId,
        CancellationToken cancellationToken,
        [FromQuery] string? type = null,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 100)
    {
        var actorUserId = User.GetUserId();

        var result = await useCase.GetReactionsAsync(
            actorUserId, postId, type, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPut("{postId:guid}/reaction")]
    public async Task<IResult> SetReactionAsync(
        [FromRoute] Guid postId,
        [FromBody] SetReactionRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await useCase.SetReactionAsync(
            actorUserId, postId, request.Type, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpDelete("{postId:guid}/reaction")]
    public async Task<IResult> RemoveReactionAsync(
        [FromRoute] Guid postId,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await useCase.RemoveReactionAsync(actorUserId, postId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }
}
