using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Fookbase.Api.Modules.Notifications.Hubs;

[Authorize]
public sealed class NotificationsHub : Hub
{
}
