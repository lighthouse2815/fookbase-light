using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Fookbase.Api.Modules.Identity.DTOs.Requests;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class LoginRequestValidationTests
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
    public void Missing_identifier_fails_validation()
    {
        var request = new LoginRequest(null, "Password123!");
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, result => result.ErrorMessage == "Email hoặc số điện thoại là bắt buộc.");
    }

    [Fact]
    public void Invalid_identifier_fails_validation()
    {
        var request = new LoginRequest("not-an-email-or-phone", "Password123!");
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, result => result.ErrorMessage == "Email hoặc số điện thoại không hợp lệ.");
    }

    [Fact]
    public void Missing_password_fails_validation()
    {
        var request = new LoginRequest("user@example.com", null);
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, result => result.ErrorMessage == "Password is required.");
    }
}
