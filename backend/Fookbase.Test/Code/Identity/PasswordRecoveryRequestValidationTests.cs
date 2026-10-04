using System.Net;
using System.Text;
using System.Text.Json;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class PasswordRecoveryRequestValidationTests(IdentityApiFactory factory)
    : IClassFixture<IdentityApiFactory>
{
    [Theory]
    [InlineData("user@example.test", null, null, "Token")]
    [InlineData("user@example.test", "", null, "Token")]
    [InlineData("user@example.test", "   ", null, "Token")]
    [InlineData("user@example.test", null, "123456", "Token")]
    [InlineData("0912345678", null, null, "Code")]
    [InlineData("0912345678", null, "", "Code")]
    [InlineData("0912345678", null, "   ", "Code")]
    [InlineData("0912345678", "reset-token", null, "Code")]
    [InlineData("user@example.test", "reset-token", null, null)]
    [InlineData("0912345678", null, "123456", null)]
    public void Reset_requires_the_credential_for_the_contact_kind(
        string identifier, string? token, string? code, string? errorField)
    {
        var request = new ResetPasswordRequest(identifier, token, "Password123!", "Password123!", code);
        var serviceCollection = new ServiceCollection().AddLogging();
        serviceCollection.AddControllers();
        using var services = serviceCollection.BuildServiceProvider();
        var context = new ActionContext(
            new DefaultHttpContext { RequestServices = services },
            new RouteData(), new ActionDescriptor(), new ModelStateDictionary());

        services.GetRequiredService<IObjectModelValidator>().Validate(context, null, string.Empty, request);

        Assert.Equal(errorField is null, context.ModelState.IsValid);
        if (errorField is not null)
        {
            var message = errorField == "Token"
                ? "Token là bắt buộc khi đặt lại mật khẩu bằng email."
                : "Mã OTP là bắt buộc khi đặt lại mật khẩu bằng số điện thoại.";
            Assert.Contains(context.ModelState[errorField]!.Errors, error => error.ErrorMessage == message);
        }
    }

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
