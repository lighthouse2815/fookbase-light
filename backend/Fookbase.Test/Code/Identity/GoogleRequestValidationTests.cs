using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class GoogleRequestValidationTests(IdentityApiFactory factory)
    : IClassFixture<IdentityApiFactory>
{
    [Theory]
    [InlineData("/api/auth/google/exchange", "Code", "Mã xác thực Google là bắt buộc.")]
    [InlineData("/api/auth/google/exchange", "Client", "Ứng dụng xác thực Google là bắt buộc.")]
    [InlineData("/api/auth/google/link", "Code", "Mã xác thực Google là bắt buộc.")]
    [InlineData("/api/auth/google/link", "Password", "Mật khẩu là bắt buộc.")]
    [InlineData("/api/auth/google/link", "Client", "Ứng dụng xác thực Google là bắt buộc.")]
    [InlineData("/api/auth/google/mobile/exchange", "Code", "Mã xác thực Google là bắt buộc.")]
    [InlineData("/api/auth/google/mobile/exchange", "Verifier", "Mã xác minh PKCE là bắt buộc.")]
    [InlineData("/api/auth/google/mobile/link", "Code", "Mã xác thực Google là bắt buộc.")]
    [InlineData("/api/auth/google/mobile/link", "Verifier", "Mã xác minh PKCE là bắt buộc.")]
    public async Task Missing_or_blank_required_field_returns_vietnamese_validation_error(
        string endpoint, string field, string message)
    {
        using var client = factory.CreateClient();
        foreach (var value in new string?[] { null, string.Empty, "   " })
        {
            var request = new Dictionary<string, string?>
            {
                ["Code"] = "completion-code",
                ["Client"] = endpoint.Contains("/mobile/", StringComparison.Ordinal) ? "mobile" : "web",
                ["Password"] = "Password123!",
                ["Verifier"] = new string('a', 64)
            };
            request[field] = value;

            using var response = await client.PostAsJsonAsync(endpoint, request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var error = document.RootElement.GetProperty("error");
            Assert.Equal("validation_failed", error.GetProperty("code").GetString());
            var fieldError = Assert.Single(error.GetProperty("details").GetProperty(field).EnumerateArray());
            Assert.Equal(message, fieldError.GetString());
        }
    }
}
