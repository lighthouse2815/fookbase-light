using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Posts.Controllers;

[ApiController]
[Authorize]
[Route("api/posts")]
public sealed class SavedPostsController(PostsUseCase useCase) : ControllerBase
{
    [HttpGet("saved")]
    public async Task<IResult> GetSavedPostsAsync(
        CancellationToken cancellationToken,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = SocialInteractionsService.DefaultPageSize)
    {
        var actorUserId = User.GetUserId();

        var result = await useCase.GetSavedPostsAsync(actorUserId, cursor, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPost("{postId:guid}/save")]
    public async Task<IResult> SavePostAsync(
        [FromRoute] Guid postId,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await useCase.SavePostAsync(actorUserId, postId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    [HttpDelete("{postId:guid}/save")]
    public async Task<IResult> RemoveSavedPostAsync(
        [FromRoute] Guid postId,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await useCase.RemoveSavedPostAsync(actorUserId, postId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }
}
