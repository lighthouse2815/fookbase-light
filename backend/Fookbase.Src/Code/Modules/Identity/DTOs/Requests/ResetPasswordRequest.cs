using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Domain.Enums;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record ResetPasswordRequest(

    [Required(ErrorMessage = "Email hoặc số điện thoại là bắt buộc.")]
    [EmailOrPhoneNumber]
    string? Identifier,

    [RequiredForContact(nameof(Identifier), ContactKind.Email,
        ErrorMessage = "Token là bắt buộc khi đặt lại mật khẩu bằng email.")]
    string? Token,

    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc.")]
    [MinLength(8, ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự.")]
    string? Password,

    [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc.")]
    [PasswordConfirmation(nameof(Password))]
    string? ConfirmPassword,

    [RequiredForContact(nameof(Identifier), ContactKind.Phone,
        ErrorMessage = "Mã OTP là bắt buộc khi đặt lại mật khẩu bằng số điện thoại.")]
    [RegularExpression("[0-9]{6}", ErrorMessage = "Mã OTP phải gồm 6 chữ số.")]
    string? Code = null);
