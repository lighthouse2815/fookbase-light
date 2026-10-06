using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Ai.DTOs.Requests;
using Fookbase.Api.Modules.Ai.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fookbase.Api.Modules.Ai.Controllers;

[ApiController]
[Authorize]
[Route("api/ai")]
[EnableRateLimiting("ai-chat")]
public sealed class AiChatController(AiChatService service) : ControllerBase
{
    [HttpPost("chat")]
    public async Task<IActionResult> ChatAsync(
        [FromBody] AiChatRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out _))
        {
            return Unauthorized();
        }

        var result = await service.ChatAsync(request, cancellationToken);
        return result.Succeeded
            ? Ok(result.Response)
            : Problem(statusCode: result.StatusCode, title: "Yêu cầu trò chuyện AI thất bại.", detail: result.Error);
    }
}
