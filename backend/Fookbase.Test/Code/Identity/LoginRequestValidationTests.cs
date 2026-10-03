using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fookbase.Api.Modules.Identity.DTOs.Requests;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class LoginRequestValidationTests(IdentityApiFactory factory)
    : IClassFixture<IdentityApiFactory>
{
    [Fact]
    public void Serializes_identifier_without_legacy_email()
    {
        var request = new LoginRequest("user@example.com", "Password123!");

        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Assert.Equal("user@example.com", document.RootElement.GetProperty("identifier").GetString());
        Assert.False(document.RootElement.TryGetProperty("email", out _));
    }

    [Fact]
    public async Task Missing_identifier_fails_validation()
    {
        var request = new LoginRequest(null, "Password123!");
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = document.RootElement.GetProperty("error");
        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        Assert.Contains(error.GetProperty("details").GetProperty("Identifier").EnumerateArray(),
            message => message.GetString() == "Email hoặc số điện thoại là bắt buộc.");
    }

    [Fact]
    public async Task Invalid_identifier_fails_validation()
    {
        var request = new LoginRequest("not-an-email-or-phone", "Password123!");
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = document.RootElement.GetProperty("error");
        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        Assert.Contains(error.GetProperty("details").GetProperty("Identifier").EnumerateArray(),
            message => message.GetString() == "Email hoặc số điện thoại không hợp lệ.");
    }

    [Fact]
    public async Task Missing_password_fails_validation()
    {
        var request = new LoginRequest("user@example.com", null);
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = document.RootElement.GetProperty("error");
        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        var message = Assert.Single(error.GetProperty("details").GetProperty("Password").EnumerateArray());
        Assert.False(string.IsNullOrWhiteSpace(message.GetString()));
    }
}
