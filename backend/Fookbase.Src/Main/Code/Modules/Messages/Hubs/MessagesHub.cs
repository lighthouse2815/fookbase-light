using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Fookbase.Api.Modules.Messages.Services;

namespace Fookbase.Api.Modules.Messages.Hubs;

[Authorize]
public sealed class MessagesHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (Guid.TryParse(Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var actorUserId))
        {
            var presence = Context.GetHttpContext()?.RequestServices.GetRequiredService<MessagesPresenceService>();
            if (presence is not null)
            {
                await presence.ConnectedAsync(actorUserId, Context.ConnectionId, Context.ConnectionAborted);
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Guid.TryParse(Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var actorUserId))
        {
            var presence = Context.GetHttpContext()?.RequestServices.GetRequiredService<MessagesPresenceService>();
            if (presence is not null)
            {
                await presence.DisconnectedAsync(actorUserId, Context.ConnectionId, CancellationToken.None);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

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
