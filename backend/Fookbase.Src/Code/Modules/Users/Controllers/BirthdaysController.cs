using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Users.Services;
using Fookbase.Api.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Users.Controllers;

[ApiController]
[Authorize]
[Route("api/birthdays")]
public sealed class BirthdaysController(UserProfileService profileService) : ControllerBase
{
    [HttpGet("today")]
    public async Task<IResult> GetTodaysBirthdaysAsync(CancellationToken cancellationToken) =>
        Results.Ok(await profileService.GetTodaysBirthdaysAsync(User.GetUserId(), cancellationToken));

    [HttpGet("upcoming")]
    public async Task<IResult> GetUpcomingBirthdaysAsync(
        [FromQuery] int? days,
        CancellationToken cancellationToken)
    {
        var result = await profileService.GetUpcomingBirthdaysAsync(User.GetUserId(), days ?? 7, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }
}
