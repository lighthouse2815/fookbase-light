using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Memories.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Memories.Controllers;

[ApiController]
[Authorize]
[Route("api/memories")]
public sealed class MemoriesController(MemoriesService service) : ControllerBase
{
    [HttpGet("today")]
    public async Task<IResult> GetTodayAsync(CancellationToken cancellationToken) =>
        Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)
            ? Results.Ok(await service.GetTodayAsync(userId, cancellationToken)) : Results.Unauthorized();
}
