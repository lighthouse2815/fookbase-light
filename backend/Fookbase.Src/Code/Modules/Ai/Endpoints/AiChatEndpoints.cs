using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Ai.DTOs.Requests;
using Fookbase.Api.Modules.Ai.Services;

namespace Fookbase.Api.Modules.Ai.Endpoints;

public static class AiChatEndpoints
{
    public static IEndpointRouteBuilder MapAiChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/ai/chat", ChatAsync)
            .RequireAuthorization()
            .RequireRateLimiting("ai-chat");
        return endpoints;
    }

    private static async Task<IResult> ChatAsync(
        AiChatRequest request,
        ClaimsPrincipal principal,
        AiChatService service,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out _))
        {
            return Results.Unauthorized();
        }

        var result = await service.ChatAsync(request, cancellationToken);
        return result.Succeeded
            ? Results.Ok(result.Response)
            : Results.Problem(statusCode: result.StatusCode, title: "Yêu cầu trò chuyện AI thất bại.", detail: result.Error);
    }
}
