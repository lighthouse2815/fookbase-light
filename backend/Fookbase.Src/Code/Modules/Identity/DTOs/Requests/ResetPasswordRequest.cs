using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record ResetPasswordRequest(
    [Required(ErrorMessage = "Email hoặc số điện thoại là bắt buộc.")]
    [EmailOrPhoneNumber]
    string? Identifier,

    string? Token,

    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc.")]
    [MinLength(8, ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự.")]
    string? Password,

    [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc.")]
    string? ConfirmPassword,

    [RegularExpression("[0-9]{6}", ErrorMessage = "Mã OTP phải gồm 6 chữ số.")]
    string? Code = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Password != ConfirmPassword)
            yield return new ValidationResult("Xác nhận mật khẩu không khớp.", [nameof(ConfirmPassword)]);

        if (string.IsNullOrWhiteSpace(Identifier)) yield break;

        if (Identifier.Contains('@'))
        {
            if (string.IsNullOrWhiteSpace(Token))
                yield return new ValidationResult("Token là bắt buộc khi đặt lại mật khẩu bằng email.", [nameof(Token)]);
        }
        else if (string.IsNullOrWhiteSpace(Code))
        {
            yield return new ValidationResult("Mã OTP là bắt buộc khi đặt lại mật khẩu bằng số điện thoại.", [nameof(Code)]);
        }
    }
}
