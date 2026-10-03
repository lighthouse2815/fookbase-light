using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Fookbase.Api.Shared.ErrorHandling;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class AuthenticationErrorResponseTests(IdentityApiFactory factory)
    : IClassFixture<IdentityApiFactory>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_login_uses_shared_credentials_error(bool accountExists)
    {
        var email = $"error-{Guid.NewGuid():N}@example.test";
        using var client = factory.CreateClient();
        if (accountExists)
        {
            using var registered = await client.PostAsJsonAsync("/api/auth/register",
                new RegisterRequest(email, $"error-{Guid.NewGuid():N}"[..32], "Password123!"));
            registered.EnsureSuccessStatusCode();
        }

        using var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "wrong-password"));

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, ErrorCode.InvalidCredentials);
    }

    [Fact]
    public async Task Incorrect_password_for_disabling_two_factor_keeps_validation_status_with_shared_error()
    {
        using var client = factory.CreateClient();
        using var registered = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"error-{Guid.NewGuid():N}@example.test", $"error-{Guid.NewGuid():N}"[..32], "Password123!"));
        var session = await registered.Content.ReadApiDataAsync<AuthenticationResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        using var response = await client.PostAsJsonAsync("/api/auth/2fa/disable",
            new DisableTwoFactorRequest("wrong-password"));

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, ErrorCode.InvalidCredentials);
    }

    [Fact]
    public async Task Invalid_refresh_token_keeps_unauthorized_status_with_shared_error()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest("invalid-refresh-token"));

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, ErrorCode.InvalidRefreshToken);
    }

    [Fact]
    public async Task Incorrect_current_password_preserves_identity_validation_details()
    {
        using var client = factory.CreateClient();
        using var registered = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"error-{Guid.NewGuid():N}@example.test", $"error-{Guid.NewGuid():N}"[..32], "Password123!"));
        var session = await registered.Content.ReadApiDataAsync<AuthenticationResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        using var response = await client.PostAsJsonAsync("/api/auth/password/change",
            new ChangePasswordRequest("wrong-password", "NewPassword123!", "NewPassword123!"));

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, ErrorCode.ValidationFailed);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var details = document.RootElement.GetProperty("error").GetProperty("details");
        Assert.NotEmpty(details.GetProperty("PasswordMismatch").EnumerateArray());
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, ErrorCode expected)
    {
        Assert.Equal(status, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = document.RootElement.GetProperty("error");
        Assert.Equal(expected.Code, error.GetProperty("code").GetString());
        Assert.Equal(expected.Message, error.GetProperty("message").GetString());
    }
}
