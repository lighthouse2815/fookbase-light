using Fookbase.Api.Modules.Identity.Entities;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class TwoFactorLoginChallengeTests
{
    [Fact]
    public void Constructor_creates_a_challenge_for_a_pending_external_login()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();

        var challenge = new TwoFactorLoginChallenge(userId, now, "Google", "google-subject");

        Assert.NotEqual(Guid.Empty, challenge.Id);
        Assert.Equal(userId, challenge.UserId);
        Assert.Equal(now, challenge.CreatedAtUtc);
        Assert.Equal(now.AddMinutes(5), challenge.ExpiresAtUtc);
        Assert.Equal("Google", challenge.PendingExternalProvider);
        Assert.Equal("google-subject", challenge.PendingExternalProviderKey);
    }

    [Fact]
    public void Constructor_rejects_an_incomplete_pending_external_login() =>
        Assert.Throws<ArgumentException>(() => new TwoFactorLoginChallenge(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Google"));
}
