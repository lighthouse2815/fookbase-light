using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.SignalR;
using Fookbase.Api.Modules.Identity.Services;

namespace Fookbase.Api.Modules.Identity.Middleware;

public sealed class AccountModerationHubFilter(AccountModerationService moderationService) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var user = invocationContext.Context.User;
        if (Guid.TryParse(user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId) &&
            await moderationService.IsUnavailableAsync(userId, invocationContext.Context.ConnectionAborted))
        {
            throw new HubException("This account cannot perform this action.");
        }

        return await next(invocationContext);
    }
}
