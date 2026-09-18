using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Fookbase.Api.Modules.Games.Services;

namespace Fookbase.Api.Modules.Games.Hubs;

[Authorize]
public sealed class FlappyBirdHub(FlappyBirdRoomService roomService) : Hub
{
    private const string RoomName = "flappy-bird";

    public async Task<FlappyBirdRound?> Join()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomName, Context.ConnectionAborted);
        return roomService.GetActiveRound();
    }

    public async Task StartRound()
    {
        var round = roomService.StartRound();
        await Clients.Group(RoomName).SendAsync("RoundStarted", round, Context.ConnectionAborted);
    }

    public async Task UpdatePlayer(FlappyBirdPlayerUpdate update)
    {
        var round = roomService.GetActiveRound();
        if (round is null || round.Id != update.RoundId ||
            !double.IsFinite(update.BirdY) || update.BirdY is < 13 or > 451 ||
            !double.IsFinite(update.Velocity) || update.Velocity is < -650 or > 650 ||
            update.Score is < 0 or > 10_000 ||
            update.Phase is not ("playing" or "over"))
        {
            return;
        }

        await Clients.OthersInGroup(RoomName).SendAsync("PlayerUpdated", new
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
        await Clients.Group(RoomName).SendAsync("PlayerLeft", new { ConnectionId = Context.ConnectionId });
        await base.OnDisconnectedAsync(exception);
    }
}

public sealed record FlappyBirdPlayerUpdate(
    Guid RoundId,
    double BirdY,
    double Velocity,
    string Phase,
    int Score);
