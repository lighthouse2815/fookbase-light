using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Fookbase.Api.Modules.Messages.Services;

namespace Fookbase.Api.Modules.Messages.Hubs;

[Authorize]
public sealed class MessagesHub : Hub
{
    public async Task Typing(Guid conversationId, MessagesService messagesService)
    {
        if (!Guid.TryParse(Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var actorUserId))
        {
            return;
        }

        await messagesService.NotifyTypingAsync(actorUserId, conversationId, Context.ConnectionAborted);
    }
}

public sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
}
