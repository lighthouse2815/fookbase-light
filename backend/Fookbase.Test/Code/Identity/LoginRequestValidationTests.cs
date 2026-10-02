using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.DTOs.Requests;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class LoginRequestValidationTests
{
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
