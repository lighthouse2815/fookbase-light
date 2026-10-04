using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record ChangePasswordRequest(
    [Required(ErrorMessage = "Mật khẩu hiện tại là bắt buộc.")]
    string? CurrentPassword,

    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc.")]
    [MinLength(8, ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự.")]
    string? NewPassword,

    [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc.")]
    string? ConfirmPassword) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (NewPassword != ConfirmPassword)
            yield return new ValidationResult("Xác nhận mật khẩu không khớp.", [nameof(ConfirmPassword)]);
    }
}
