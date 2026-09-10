using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Fookbase.Api.Modules.Messages.Hubs;

[Authorize]
public sealed class MessagesHub : Hub
{
}

public sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
}
