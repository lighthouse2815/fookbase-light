using Fookbase.Api.Modules.Games.Services;
using Microsoft.AspNetCore.Authorization;

namespace Fookbase.Api.Modules.Games.Hubs;

[Authorize]
public sealed class FlappyBirdHub([FromKeyedServices("flappy-bird")] GameRoomService roomService)
    : GameRoomHub(roomService, "flappy-bird")
{
    public Task UpdatePlayer(FlappyBirdPlayerUpdate update)
    {
        if (!double.IsFinite(update.BirdY) || update.BirdY is < 13 or > 451 ||
            !double.IsFinite(update.Velocity) || update.Velocity is < -650 or > 650 ||
            update.Score is < 0 or > 10_000 || update.Phase is not ("playing" or "over"))
        {
            return Task.CompletedTask;
        }

        return BroadcastPlayerAsync(update.RoundId, new
        {
            ConnectionId = Context.ConnectionId,
            update.BirdY, update.Velocity, update.Phase, update.Score
        });
    }
}

public sealed record FlappyBirdPlayerUpdate(Guid RoundId, double BirdY, double Velocity, string Phase, int Score);
