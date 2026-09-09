using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class AuthenticationEndpointsTests(IdentityApiFactory factory)
    : IClassFixture<IdentityApiFactory>
{
    [Fact]
    public async Task Register_with_malformed_json_returns_bad_request()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent("{", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/auth/register", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_succeeds_and_stores_only_hashed_secrets()
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(account.Email, account.Username, account.Password));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var authentication = await ReadAuthenticationResponseAsync(response);
        Assert.Equal(account.Email, authentication.User.Email);
        Assert.Equal(account.Username, authentication.User.Username);
        Assert.False(string.IsNullOrWhiteSpace(authentication.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(authentication.RefreshToken));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(authentication.AccessToken);
        Assert.Equal(authentication.User.Id.ToString(), jwt.Subject);
        Assert.Equal(account.Email, jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(account.Username, jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.UniqueName).Value);
        Assert.False(string.IsNullOrWhiteSpace(jwt.Id));

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = await dbContext.Users.SingleAsync(item => item.Id == authentication.User.Id);
        var refreshToken = await dbContext.RefreshTokens
            .SingleAsync(item => item.UserId == authentication.User.Id);
        Assert.NotEqual(account.Password, user.PasswordHash);
        Assert.Equal(account.Email.ToUpperInvariant(), user.NormalizedEmail);
        Assert.Equal(account.Username.ToUpperInvariant(), user.NormalizedUserName);
        Assert.NotEqual(authentication.RefreshToken, refreshToken.TokenHash);
        Assert.Equal(Hash(authentication.RefreshToken), refreshToken.TokenHash);
    }

    [Fact]
    public async Task Register_with_duplicate_email_returns_conflict()
    {
        var first = CreateUniqueAccount();
        var second = CreateUniqueAccount() with { Email = first.Email };
        using var client = factory.CreateClient();

        await RegisterAsync(client, first);
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(second.Email, second.Username, second.Password));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_duplicate_username_returns_conflict()
    {
        var first = CreateUniqueAccount();
        var second = CreateUniqueAccount() with { Username = first.Username };
        using var client = factory.CreateClient();

        await RegisterAsync(client, first);
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(second.Email, second.Username, second.Password));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_correct_password_returns_token_pair()
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();
        await RegisterAsync(client, account);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(account.Email, account.Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authentication = await ReadAuthenticationResponseAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(authentication.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(authentication.RefreshToken));
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_unauthorized()
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();
        await RegisterAsync(client, account);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(account.Email, "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_without_access_token_returns_unauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_with_valid_access_token_returns_authenticated_user()
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();
        var authentication = await RegisterAsync(client, account);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authentication.AccessToken);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<AuthenticatedUserResponse>();
        Assert.NotNull(user);
        Assert.Equal(authentication.User, user);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_old_token_cannot_be_reused()
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();
        var original = await RegisterAsync(client, account);

        var refreshResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshRequest(original.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var rotated = await ReadAuthenticationResponseAsync(refreshResponse);
        Assert.NotEqual(original.AccessToken, rotated.AccessToken);
        Assert.NotEqual(original.RefreshToken, rotated.RefreshToken);

        var reuseResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshRequest(original.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token_and_revoked_token_cannot_refresh()
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();
        var authentication = await RegisterAsync(client, account);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authentication.AccessToken);

        var logoutResponse = await client.PostAsJsonAsync(
            "/api/auth/logout",
            new LogoutRequest(authentication.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var refreshResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshRequest(authentication.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    private static async Task<AuthenticationResponse> RegisterAsync(
        HttpClient client,
        TestAccount account)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(account.Email, account.Username, account.Password));

        response.EnsureSuccessStatusCode();
        return await ReadAuthenticationResponseAsync(response);
    }

    private static async Task<AuthenticationResponse> ReadAuthenticationResponseAsync(
        HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<AuthenticationResponse>()
        ?? throw new InvalidOperationException("Authentication response body was empty.");

    private static TestAccount CreateUniqueAccount()
    {
        var suffix = Guid.NewGuid().ToString("N")[..16];
        return new TestAccount(
            $"user-{suffix}@example.com",
            $"user_{suffix}",
            "Password123!");
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private sealed record TestAccount(string Email, string Username, string Password);
}
