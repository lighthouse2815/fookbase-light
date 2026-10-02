namespace Fookbase.Api.Modules.Identity.Common;

internal static class IdentityChallengeWindow
{
    public static (DateTimeOffset StartedAtUtc, int SendCount) ResetIfExpired(
        DateTimeOffset startedAtUtc,
        int sendCount,
        DateTimeOffset now) =>
        startedAtUtc.AddHours(1) <= now ? (now, 0) : (startedAtUtc, sendCount);
}
