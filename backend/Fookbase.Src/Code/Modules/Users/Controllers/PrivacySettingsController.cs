using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Users.DTOs.Requests;
using Fookbase.Api.Modules.Users.Services;
using Fookbase.Api.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Users.Controllers;

[ApiController]
[Authorize]
[Route("api/privacy")]
public sealed class PrivacySettingsController(UserPrivacySettingsService privacySettingsService) : ControllerBase
{
    [HttpGet]
    public async Task<IResult> GetAsync(CancellationToken cancellationToken)
    {
        var result = await privacySettingsService.GetAsync(User.GetUserId(), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    [HttpPatch]
    public async Task<IResult> UpdateAsync(
        [FromBody] UpdatePrivacySettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await privacySettingsService.UpdateAsync(User.GetUserId(), request, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }
}
