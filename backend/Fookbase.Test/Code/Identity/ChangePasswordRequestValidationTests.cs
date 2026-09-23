using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.DTOs.Requests;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class ChangePasswordRequestValidationTests
{
    [Fact]
    public void Mismatched_confirmation_fails_validation()
    {
        var request = new ChangePasswordRequest("Current-password-123!", "New-password-123!", "different-password");
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, result =>
            result.ErrorMessage == "Password confirmation does not match the new password.");
    }
}
