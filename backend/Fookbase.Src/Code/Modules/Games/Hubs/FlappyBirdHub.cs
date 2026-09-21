using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Games.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Fookbase.Api.Modules.Games.Hubs;

[Authorize]
public sealed class FlappyBirdHub(FlappyBirdRoomService roomService) : Hub
{
    public async Task<FlappyBirdRoomMembership> CreateRoom()
    {
        var membership = roomService.CreateRoom(GetUserId(), Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(membership.Code), Context.ConnectionAborted);
        return membership;
    }

    public async Task<FlappyBirdRoomMembership> JoinRoom(string roomCode)
    {
        var membership = roomService.JoinRoom(NormalizeRoomCode(roomCode), GetUserId(), Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(membership.Code), Context.ConnectionAborted);
        await Clients.Group(GroupName(membership.Code)).SendAsync(
            "RoomUpdated",
            new FlappyBirdRoomUpdate(membership.Code, membership.PlayerCount),
            Context.ConnectionAborted);
        return membership;
    }

    public Task<FlappyBirdRoomMembership?> RejoinRoom() =>
        Task.FromResult(roomService.GetMembership(Context.ConnectionId, GetUserId()));

    public async Task LeaveRoom()
    {
        var update = roomService.LeaveRoom(Context.ConnectionId);
        if (update is null)
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(update.Code), Context.ConnectionAborted);
        await NotifyRoomDepartureAsync(update);
    }

    public async Task StartRound()
    {
        var membership = roomService.StartRound(GetUserId(), Context.ConnectionId);
        await Clients.Group(GroupName(membership.Code)).SendAsync("RoundStarted", membership.Round!, Context.ConnectionAborted);
    }

    public async Task UpdatePlayer(FlappyBirdPlayerUpdate update)
    {
        if (!double.IsFinite(update.BirdY) || update.BirdY is < 13 or > 451 ||
            !double.IsFinite(update.Velocity) || update.Velocity is < -650 or > 650 ||
            update.Score is < 0 or > 10_000 ||
            update.Phase is not ("playing" or "over"))
        {
            return;
        }

        var roomCode = roomService.GetRoomCodeForPlayerUpdate(Context.ConnectionId, update.RoundId);
        if (roomCode is null)
        {
            return;
        }

        await Clients.OthersInGroup(GroupName(roomCode)).SendAsync("PlayerUpdated", new
        {
            ConnectionId = Context.ConnectionId,
            update.BirdY,
            update.Velocity,
            update.Phase,
            update.Score
        }, Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var update = roomService.LeaveRoom(Context.ConnectionId);
        if (update is not null)
        {
            await NotifyRoomDepartureAsync(update);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task NotifyRoomDepartureAsync(FlappyBirdRoomUpdate update)
    {
        await Clients.Group(GroupName(update.Code)).SendAsync("PlayerLeft", new { ConnectionId = Context.ConnectionId });
        await Clients.Group(GroupName(update.Code)).SendAsync("RoomUpdated", update);
    }

    private Guid GetUserId() =>
        Guid.TryParse(Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)
            ? userId
            : throw new HubException("Unauthenticated user.");

    private static string NormalizeRoomCode(string roomCode)
    {
        var normalized = roomCode.Trim().ToUpperInvariant();
        if (normalized.Length != 6 || normalized.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new HubException("Mã phòng gồm 6 ký tự chữ hoặc số.");
        }

        return normalized;
    }

    private static string GroupName(string roomCode) => $"flappy-bird:{roomCode}";
}

public sealed record FlappyBirdPlayerUpdate(
    Guid RoundId,
    double BirdY,
    double Velocity,
    string Phase,
    int Score);
