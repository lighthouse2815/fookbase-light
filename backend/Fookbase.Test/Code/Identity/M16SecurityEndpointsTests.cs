using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fookbase.Api.Modules.Friends.Entities;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class M16SecurityEndpointsTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    [Fact]
    public async Task Friends_of_friends_policy_rejects_unrelated_and_allows_mutual_friend()
    {
        using var client = factory.CreateClient();
        var receiver = await RegisterAsync(client);
        var sender = await RegisterAsync(client);
        var mutual = await RegisterAsync(client);
        using var receiverClient = AuthenticatedClient(receiver);
        var settings = await receiverClient.PatchAsJsonAsync("/api/privacy", new { friendRequestPolicy = "friendsOfFriends" });
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);

        using var senderClient = AuthenticatedClient(sender);
        var rejected = await senderClient.PostAsync($"/api/friends/requests/{receiver.User.Id}", null);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            var now = DateTimeOffset.UtcNow;
            db.Friendships.AddRange(
                Friendship.Create(Guid.NewGuid(), sender.User.Id, mutual.User.Id, now),
                Friendship.Create(Guid.NewGuid(), receiver.User.Id, mutual.User.Id, now));
            await db.SaveChangesAsync();
        }

        var allowed = await senderClient.PostAsync($"/api/friends/requests/{receiver.User.Id}", null);
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
    }

    [Fact]
    public async Task Revoking_other_session_disables_its_refresh_without_affecting_current_session()
    {
        using var client = factory.CreateClient();
        var sessionA = await RegisterAsync(client);
        var sessionB = await LoginAsync(client, sessionA.User.Email!, TestPassword);
        using var current = AuthenticatedClient(sessionA);
        var sessions = await (await current.GetAsync("/api/auth/sessions")).Content.ReadApiDataAsync<List<AuthSessionResponse>>();
        Assert.NotNull(sessions);
        var revoked = Assert.Single(sessions!, item => !item.IsCurrent);
        Assert.Equal(HttpStatusCode.OK, (await current.DeleteAsync($"/api/auth/sessions/{revoked.SessionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(sessionB.RefreshToken))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(sessionA.RefreshToken))).StatusCode);
    }

    [Fact]
    public async Task Password_change_rejects_old_password_and_revokes_other_session_refresh()
    {
        using var client = factory.CreateClient();
        var sessionA = await RegisterAsync(client);
        var sessionB = await LoginAsync(client, sessionA.User.Email!, TestPassword);
        const string newPassword = "NewPassword123!";
        using var current = AuthenticatedClient(sessionA);
        var changed = await current.PostAsJsonAsync("/api/auth/password/change",
            new ChangePasswordRequest(TestPassword, newPassword, newPassword));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(sessionA.User.Email, TestPassword))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(sessionB.RefreshToken))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(sessionA.User.Email, newPassword))).StatusCode);
    }

    [Fact]
    public async Task Two_factor_login_returns_challenge_before_issuing_tokens_then_verifies_totp()
    {
        using var client = factory.CreateClient();
        var account = await RegisterAsync(client);
        var code = await EnableTwoFactorForTestAsync(account.User.Id);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(account.User.Email, TestPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var challenge = await login.Content.ReadApiDataAsync<TwoFactorChallengeResponse>();
        Assert.NotNull(challenge);
        Assert.True(challenge!.TwoFactorRequired);
        var verified = await client.PostAsJsonAsync("/api/auth/2fa/verify", new TwoFactorVerifyRequest(challenge.Challenge, code));
        Assert.True(verified.StatusCode == HttpStatusCode.OK, await verified.Content.ReadAsStringAsync());
        var tokens = await verified.Content.ReadApiDataAsync<AuthenticationResponse>();
        Assert.False(string.IsNullOrWhiteSpace(tokens!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
    }

    [Fact]
    public async Task Recovery_code_completes_two_factor_login_once_only()
    {
        using var client = factory.CreateClient();
        var account = await RegisterAsync(client);
        var recoveryCode = await EnableTwoFactorForTestAsync(account.User.Id, recovery: true);
        var firstChallenge = await GetChallengeAsync(client, account.User.Email!);
        var firstVerification = await client.PostAsJsonAsync("/api/auth/2fa/verify", new TwoFactorVerifyRequest(firstChallenge, recoveryCode));
        Assert.True(firstVerification.StatusCode == HttpStatusCode.OK, await firstVerification.Content.ReadAsStringAsync());
        var secondChallenge = await GetChallengeAsync(client, account.User.Email!);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/2fa/verify", new TwoFactorVerifyRequest(secondChallenge, recoveryCode))).StatusCode);
    }

    [Fact]
    public async Task Google_link_waits_for_two_factor_verification_before_adding_login()
    {
        using var client = factory.CreateClient();
        var account = await RegisterAsync(client);
        var code = await EnableTwoFactorForTestAsync(account.User.Id);
        var providerKey = $"google-sub-{Guid.NewGuid():N}";

        using var scope = factory.Services.CreateScope();
        var googleAuthentication = scope.ServiceProvider.GetRequiredService<GoogleAuthenticationService>();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await manager.FindByIdAsync(account.User.Id.ToString())
            ?? throw new InvalidOperationException();
        var completion = await googleAuthentication.CreateCompletionAsync(
            "web",
            providerKey,
            account.User.Email!,
            emailVerified: true);
        Assert.NotNull(completion);

        var linked = await googleAuthentication.LinkExistingAsync(
            completion.Code,
            "web",
            TestPassword,
            null);
        var challenge = Assert.IsType<TwoFactorChallengeResponse>(linked);
        Assert.DoesNotContain(await manager.GetLoginsAsync(user), login => login.LoginProvider == "Google");

        var verified = await client.PostAsJsonAsync(
            "/api/auth/2fa/verify",
            new TwoFactorVerifyRequest(challenge.Challenge, code));
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Assert.Contains(await manager.GetLoginsAsync(user), login =>
            login.LoginProvider == "Google" && login.ProviderKey == providerKey);
    }

    private async Task<string> EnableTwoFactorForTestAsync(Guid userId, bool recovery = false)
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await manager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException();
        await manager.ResetAuthenticatorKeyAsync(user);
        await manager.SetTwoFactorEnabledAsync(user, true);
        if (recovery)
        {
            return (await manager.GenerateNewTwoFactorRecoveryCodesAsync(user, 2) ?? []).First();
        }
        var key = await manager.GetAuthenticatorKeyAsync(user) ?? throw new InvalidOperationException();
        return new Totp(Base32Encoding.ToBytes(key)).ComputeTotp();
    }

    private static async Task<string> GetChallengeAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, TestPassword));
        var challenge = await response.Content.ReadApiDataAsync<TwoFactorChallengeResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return challenge!.Challenge;
    }

    private HttpClient AuthenticatedClient(AuthenticationResponse authentication)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authentication.AccessToken);
        return client;
    }

    private async Task<AuthenticationResponse> RegisterAsync(HttpClient client)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest($"m16-{suffix}@example.test", $"m16{suffix}", TestPassword));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadApiDataAsync<AuthenticationResponse>())!;
    }

    private static async Task<AuthenticationResponse> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadApiDataAsync<AuthenticationResponse>())!;
    }

    private const string TestPassword = "Password123!";
}
