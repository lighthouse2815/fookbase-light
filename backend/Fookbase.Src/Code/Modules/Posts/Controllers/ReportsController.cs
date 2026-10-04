using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Posts.DTOs.Requests;
using Fookbase.Api.Modules.Posts.Services;
using Fookbase.Api.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Posts.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController(ReportsService reportsService) : ControllerBase
{
    [HttpPost("users/{userId:guid}")]
    public async Task<IResult> ReportUserAsync(
        [FromRoute] Guid userId,
        [FromBody] CreateReportRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var result = await reportsService.ReportUserAsync(actorUserId, userId, request, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/reports/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    [HttpPost("posts/{postId:guid}")]
    public async Task<IResult> ReportPostAsync(
        [FromRoute] Guid postId,
        [FromBody] CreateReportRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var result = await reportsService.ReportPostAsync(actorUserId, postId, request, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/reports/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }
}
