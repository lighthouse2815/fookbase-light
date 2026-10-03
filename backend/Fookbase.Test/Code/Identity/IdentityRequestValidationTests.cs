using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class IdentityRequestValidationTests(IdentityApiFactory factory)
    : IClassFixture<IdentityApiFactory>
{
    [Theory]
    [InlineData("/api/auth/register", "{}", "Email")]
    [InlineData("/api/auth/register", "{\"email\":\"invalid\",\"username\":\"valid.name\",\"password\":\"Password123!\"}", "Email")]
    [InlineData("/api/auth/register", "{\"email\":\"valid@example.test\",\"username\":\"ab\",\"password\":\"Password123!\"}", "Username")]
    [InlineData("/api/auth/register", "{\"email\":\"valid@example.test\",\"username\":\"valid.name\",\"password\":\"short\"}", "Password")]
    [InlineData("/api/auth/registration/start", "{}", "Contact")]
    [InlineData("/api/auth/registration/start", "{\"firstName\":\"An\",\"lastName\":\"Nguyễn\",\"dateOfBirth\":\"2000-01-02\",\"gender\":\"male\",\"contact\":\"invalid\",\"password\":\"Password123!\"}", "Contact")]
    [InlineData("/api/auth/registration/start", "{\"firstName\":\"An\",\"lastName\":\"Nguyễn\",\"dateOfBirth\":\"2000-01-02\",\"gender\":\"male\",\"contact\":\"0123456789\",\"password\":\"Password123!\"}", "Contact")]
    [InlineData("/api/auth/registration/start", "{\"firstName\":\"   \",\"lastName\":\"Nguyễn\",\"dateOfBirth\":\"2000-01-02\",\"gender\":\"male\",\"contact\":\"valid@example.test\",\"password\":\"Password123!\"}", "FirstName")]
    [InlineData("/api/auth/registration/start", "{\"firstName\":\"An\",\"lastName\":\"Nguyễn\",\"gender\":\"male\",\"contact\":\"valid@example.test\",\"password\":\"Password123!\"}", "DateOfBirth")]
    [InlineData("/api/auth/registration/start", "{\"firstName\":\"An\",\"lastName\":\"Nguyễn\",\"dateOfBirth\":\"2000-01-02\",\"contact\":\"valid@example.test\",\"password\":\"Password123!\"}", "Gender")]
    [InlineData("/api/auth/registration/start", "{\"firstName\":\"An\",\"lastName\":\"Nguyễn\",\"dateOfBirth\":\"2000-01-02\",\"gender\":\"male\",\"contact\":\"valid@example.test\",\"password\":\"short\"}", "Password")]
    [InlineData("/api/auth/registration/verify", "{}", "ChallengeId")]
    [InlineData("/api/auth/registration/verify", "{\"challengeId\":\"00000000-0000-0000-0000-000000000000\",\"code\":\"123456\"}", "ChallengeId")]
    [InlineData("/api/auth/registration/verify", "{\"challengeId\":\"11111111-1111-1111-1111-111111111111\"}", "Code")]
    [InlineData("/api/auth/registration/verify", "{\"challengeId\":\"11111111-1111-1111-1111-111111111111\",\"code\":\"12345\"}", "Code")]
    [InlineData("/api/auth/registration/verify", "{\"challengeId\":\"11111111-1111-1111-1111-111111111111\",\"code\":\"12345a\"}", "Code")]
    [InlineData("/api/auth/registration/resend", "{}", "ChallengeId")]
    [InlineData("/api/auth/email/verify", "{}", "Email")]
    [InlineData("/api/auth/email/verify", "{\"email\":\"invalid\",\"token\":\"opaque-token\"}", "Email")]
    [InlineData("/api/auth/email/verify", "{\"email\":\"valid@example.test\"}", "Token")]
    [InlineData("/api/auth/password/reset", "{\"identifier\":\"valid@example.test\",\"token\":\"opaque-token\",\"confirmPassword\":\"Password123!\"}", "Password")]
    [InlineData("/api/auth/password/reset", "{\"identifier\":\"valid@example.test\",\"token\":\"opaque-token\",\"password\":\"short\",\"confirmPassword\":\"short\"}", "Password")]
    [InlineData("/api/auth/password/reset", "{\"identifier\":\"valid@example.test\",\"token\":\"opaque-token\",\"password\":\"Password123!\"}", "ConfirmPassword")]
    [InlineData("/api/auth/password/reset", "{\"identifier\":\"valid@example.test\",\"token\":\"opaque-token\",\"password\":\"Password123!\",\"confirmPassword\":\"Different123!\"}", "ConfirmPassword")]
    [InlineData("/api/auth/password/reset", "{\"identifier\":\"valid@example.test\",\"password\":\"Password123!\",\"confirmPassword\":\"Password123!\"}", "Token")]
    [InlineData("/api/auth/password/reset", "{\"identifier\":\"0912345678\",\"password\":\"Password123!\",\"confirmPassword\":\"Password123!\"}", "Code")]
    [InlineData("/api/auth/password/reset", "{\"identifier\":\"0912345678\",\"code\":\"12345a\",\"password\":\"Password123!\",\"confirmPassword\":\"Password123!\"}", "Code")]
    public async Task Invalid_request_returns_field_validation_errors(string endpoint, string body, string field)
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(endpoint, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = document.RootElement.GetProperty("error");
        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        Assert.NotEmpty(error.GetProperty("details").GetProperty(field).EnumerateArray());
    }

    [Theory]
    [InlineData("FirstName", 51)]
    [InlineData("LastName", 51)]
    [InlineData("Username", 33)]
    [InlineData("Password", 129)]
    public async Task Registration_rejects_overlong_fields(string field, int length)
    {
        var value = new string('a', length);
        var legacyRegistration = field is "Username" or "Password";
        object request = legacyRegistration
            ? new RegisterRequest("valid@example.test", field == "Username" ? value : "valid.name",
                field == "Password" ? value : "Password123!")
            : new RegistrationStartRequest(field == "FirstName" ? value : "An",
                field == "LastName" ? value : "Nguyễn", new DateOnly(2000, 1, 2),
                "male", "valid@example.test", "Password123!");
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            legacyRegistration ? "/api/auth/register" : "/api/auth/registration/start", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = document.RootElement.GetProperty("error");
        Assert.Equal("validation_failed", error.GetProperty("code").GetString());
        Assert.NotEmpty(error.GetProperty("details").GetProperty(field).EnumerateArray());
    }

    [Fact]
    public async Task Registration_preserves_trimmed_name_lengths_and_phone_normalization()
    {
        var phone = $"09{RandomNumberGenerator.GetInt32(10_000_000, 99_999_999)}";
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/registration/start",
            new RegistrationStartRequest($" {new string('a', 50)} ", $" {new string('b', 50)} ",
                new DateOnly(2000, 1, 2), "male", $" {phone[..3]} {phone[3..6]} {phone[6..]} ", "Password123!"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(await response.Content.ReadApiDataAsync<RegistrationChallengeResponse>());
        Assert.Equal(6, factory.Services.GetRequiredService<TestContactOtpSender>().LastCodeFor($"+84{phone[1..]}").Length);
    }
}
