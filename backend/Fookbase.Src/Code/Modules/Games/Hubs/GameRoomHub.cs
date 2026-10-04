using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Games.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Fookbase.Api.Modules.Games.Hubs;

[Authorize]
public abstract class GameRoomHub(GameRoomService roomService, string gameId) : Hub
{
    public async Task<GameRoomMembership> CreateRoom()
    {
        var membership = RunRoomAction(() => roomService.CreateRoom(GetUserId(), Context.ConnectionId));
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(membership.Code), Context.ConnectionAborted);
        return membership;
    }

    public async Task<GameRoomMembership> JoinRoom(string roomCode)
    {
        var normalized = roomCode.Trim().ToUpperInvariant();
        if (normalized.Length != 6 || normalized.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new HubException("Mã phòng gồm 6 ký tự chữ hoặc số.");
        }

        var membership = RunRoomAction(() => roomService.JoinRoom(normalized, GetUserId(), Context.ConnectionId));
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(membership.Code), Context.ConnectionAborted);
        await Clients.Group(GroupName(membership.Code)).SendAsync("RoomUpdated",
            new GameRoomUpdate(membership.Code, membership.PlayerCount, membership.HostUserId), Context.ConnectionAborted);
        return membership;
    }

    public Task<GameRoomMembership?> RejoinRoom() =>
        Task.FromResult(roomService.GetMembership(Context.ConnectionId, GetUserId()));

    public async Task LeaveRoom()
    {
        var update = roomService.LeaveRoom(Context.ConnectionId);
        if (update is null) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(update.Code), Context.ConnectionAborted);
        await NotifyRoomDepartureAsync(update);
    }

    public async Task StartRound()
    {
        var membership = RunRoomAction(() => roomService.StartRound(GetUserId(), Context.ConnectionId));
        await Clients.Group(GroupName(membership.Code)).SendAsync("RoundStarted", membership.Round!, Context.ConnectionAborted);
    }

    protected async Task BroadcastPlayerAsync(Guid roundId, object update)
    {
        var roomCode = roomService.GetRoomCodeForPlayerUpdate(Context.ConnectionId, roundId);
        if (roomCode is not null)
        {
            await Clients.OthersInGroup(GroupName(roomCode)).SendAsync("PlayerUpdated", update, Context.ConnectionAborted);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var update = roomService.LeaveRoom(Context.ConnectionId);
        if (update is not null) await NotifyRoomDepartureAsync(update);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task NotifyRoomDepartureAsync(GameRoomUpdate update)
    {
        await Clients.Group(GroupName(update.Code)).SendAsync("PlayerLeft", new { ConnectionId = Context.ConnectionId });
        await Clients.Group(GroupName(update.Code)).SendAsync("RoomUpdated", update);
    }

    private Guid GetUserId() =>
        Guid.TryParse(Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)
            ? userId
            : throw new HubException("Unauthenticated user.");

    private static GameRoomMembership RunRoomAction(Func<GameRoomMembership> action)
    {
        try { return action(); }
        catch (GameRoomException exception) { throw new HubException(exception.Message); }
    }

    private string GroupName(string roomCode) => $"{gameId}:{roomCode}";
}
