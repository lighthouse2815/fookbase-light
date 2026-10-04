using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record ChangePasswordRequest(
    [Required(ErrorMessage = "Current password is required.")]
    string? CurrentPassword,

    [Required(ErrorMessage = "New password is required.")]
    [MinLength(8, ErrorMessage = "New password must be at least 8 characters long.")]
    string? NewPassword,

    [Required(ErrorMessage = "Password confirmation is required.")]
    string? ConfirmPassword) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (NewPassword != ConfirmPassword)
            yield return new ValidationResult("Password confirmation does not match the new password.", [nameof(ConfirmPassword)]);
    }
}
