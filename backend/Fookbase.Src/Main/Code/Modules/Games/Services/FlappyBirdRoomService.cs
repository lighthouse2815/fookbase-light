using System.Security.Cryptography;

namespace Fookbase.Api.Modules.Games.Services;

public sealed class FlappyBirdRoomService(TimeProvider timeProvider)
{
    private readonly object syncRoot = new();
    private FlappyBirdRound? activeRound;

    public FlappyBirdRound StartRound()
    {
        lock (syncRoot)
        {
            activeRound = new FlappyBirdRound(
                Guid.NewGuid(),
                RandomNumberGenerator.GetInt32(int.MaxValue),
                timeProvider.GetUtcNow().AddSeconds(3));
            return activeRound;
        }
    }

    public FlappyBirdRound? GetActiveRound()
    {
        lock (syncRoot)
        {
            return activeRound;
        }
    }
}

public sealed record FlappyBirdRound(Guid Id, int Seed, DateTimeOffset StartsAtUtc);
