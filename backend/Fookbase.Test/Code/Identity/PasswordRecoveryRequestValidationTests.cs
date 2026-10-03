using System.Net;
using System.Text;
using System.Text.Json;
using Fookbase.Api.Modules.Identity.DTOs.Requests;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class PasswordRecoveryRequestValidationTests(IdentityApiFactory factory)
    : IClassFixture<IdentityApiFactory>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Serializes_only_identifier_for_email_or_phone(bool reset)
    {
        object request = reset
            ? new ResetPasswordRequest("user@example.test", "token", "Password123!", "Password123!")
            : new ForgotPasswordRequest("user@example.test");

        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Assert.Equal("user@example.test", document.RootElement.GetProperty("identifier").GetString());
        Assert.False(document.RootElement.TryGetProperty("email", out _));
        Assert.False(document.RootElement.TryGetProperty("effectiveIdentifier", out _));
    }

    [Theory]
    [InlineData("/api/auth/password/forgot", "{}")]
    [InlineData("/api/auth/password/forgot", "{\"identifier\":null}")]
    [InlineData("/api/auth/password/forgot", "{\"identifier\":\"\"}")]
    [InlineData("/api/auth/password/forgot", "{\"identifier\":\"   \"}")]
    [InlineData("/api/auth/password/forgot", "{\"identifier\":\"invalid-contact\"}")]
    [InlineData("/api/auth/password/forgot", "{\"identifier\":\"0123456789\"}")]
    [InlineData("/api/auth/password/forgot", "{\"email\":\"user@example.test\"}")]
    [InlineData("/api/auth/password/reset", "{}")]
    [InlineData("/api/auth/password/reset", "{\"identifier\":null}")]
    [InlineData("/api/auth/password/reset", "{\"identifier\":\"\"}")]
    [InlineData("/api/auth/password/reset", "{\"identifier\":\"   \"}")]
    [InlineData("/api/auth/password/reset", "{\"identifier\":\"invalid-contact\"}")]
    [InlineData("/api/auth/password/reset", "{\"identifier\":\"0123456789\"}")]
    [InlineData("/api/auth/password/reset", "{\"email\":\"user@example.test\"}")]
    public async Task Missing_or_invalid_identifier_returns_validation_error(string endpoint, string body)
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(endpoint, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = document.RootElement.GetProperty("error");
        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        Assert.NotEmpty(error.GetProperty("details").GetProperty("Identifier").EnumerateArray());
    }
}
