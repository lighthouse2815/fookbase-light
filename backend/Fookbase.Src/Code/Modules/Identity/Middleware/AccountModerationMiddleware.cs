using System.IdentityModel.Tokens.Jwt;
using Fookbase.Api.Modules.Admin.Services;

namespace Fookbase.Api.Modules.Identity.Middleware;

public sealed class AccountModerationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AccountModerationService moderationService)
    {
        if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method) ||
            HttpMethods.IsOptions(context.Request.Method) || context.User.Identity?.IsAuthenticated != true ||
            context.Request.Path.StartsWithSegments("/api/admin") || context.Request.Path.StartsWithSegments("/api/auth"))
        {
            await next(context);
            return;
        }

        if (!Guid.TryParse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId) ||
            !await moderationService.IsUnavailableAsync(userId, context.RequestAborted))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { code = "account_unavailable", message = "This account cannot perform this action." });
    }
}
