using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Games.Services;
using Microsoft.AspNetCore.Authorization;

namespace Fookbase.Api.Modules.Games.Hubs;

[Authorize]
public sealed class JumpingHub([FromKeyedServices("jumping")] GameRoomService roomService)
    : GameRoomHub(roomService, "jumping")
{
    public Task UpdatePlayer(JumpingPlayerUpdate update)
    {
        if (!double.IsFinite(update.Height) || update.Height is < 0 or > 220 ||
            update.Score is < 0 or > 100_000 || update.Phase is not ("playing" or "over"))
        {
            return Task.CompletedTask;
        }

        return BroadcastPlayerAsync(update.RoundId, new
        {
            ConnectionId = Context.ConnectionId,
            Username = Context.User?.FindFirstValue(JwtRegisteredClaimNames.UniqueName) ?? "Bạn chơi",
            update.RoundId, update.Height, update.Phase, update.Score
        });
    }
}

public sealed record JumpingPlayerUpdate(Guid RoundId, double Height, string Phase, int Score);
