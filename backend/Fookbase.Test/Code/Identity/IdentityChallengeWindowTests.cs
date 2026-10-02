using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class IdentityChallengeWindowTests
{
    [Fact]
    public void Reset_if_expired_resets_the_window_at_its_one_hour_boundary()
    {
        var startedAt = DateTimeOffset.UtcNow;

        var active = IdentityChallengeWindow.ResetIfExpired(startedAt, 3, startedAt.AddMinutes(59));
        var expired = IdentityChallengeWindow.ResetIfExpired(startedAt, 3, startedAt.AddHours(1));

        Assert.Equal((startedAt, 3), active);
        Assert.Equal((startedAt.AddHours(1), 0), expired);
    }
}
