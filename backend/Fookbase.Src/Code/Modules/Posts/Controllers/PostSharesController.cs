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
public sealed class PostSharesController(PostsUseCase useCase) : ControllerBase
{
    [HttpPost("{postId:guid}/shares")]
    public async Task<IResult> SharePostAsync(
        [FromRoute] Guid postId,
        [FromBody] CreatePostShareRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        var result = await useCase.SharePostAsync(
            actorUserId,
            postId,
            request.DestinationType,
            request.DestinationId,
            request.Caption,
            cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/posts/{postId}/shares/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }
}
