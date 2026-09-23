using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record ChangePasswordRequest(
    [property: Required(ErrorMessage = "Current password is required.")]
    string? CurrentPassword,
    [property: Required(ErrorMessage = "New password is required.")]
    [property: MinLength(8, ErrorMessage = "New password must be at least 8 characters long.")]
    string? NewPassword,
    [property: Required(ErrorMessage = "Password confirmation is required.")]
    [property: Compare("NewPassword", ErrorMessage = "Password confirmation does not match the new password.")]
    string? ConfirmPassword)
{
    // MVC can bind through this constructor and validate the property attributes, including Compare.
    public ChangePasswordRequest() : this(null, null, null)
    {
    }
}
