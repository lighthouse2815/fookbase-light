using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Posts.Controllers;

[ApiController]
[Route("api/hashtags")]
public sealed class HashtagsController(PostsUseCase useCase) : ControllerBase
{
    [HttpGet("{tag}/posts")]
    [AllowAnonymous]
    public async Task<IResult> GetHashtagPostsAsync(
        [FromRoute] string tag,
        CancellationToken cancellationToken,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = SocialInteractionsService.DefaultPageSize)
    {
        var viewerUserId = User.Identity?.IsAuthenticated == true ? User.GetUserId() : (Guid?)null;

        var result = await useCase.GetHashtagPostsAsync(viewerUserId, tag, cursor, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }
}
