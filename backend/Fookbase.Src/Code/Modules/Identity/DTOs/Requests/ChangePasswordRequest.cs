using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record ChangePasswordRequest(
    [Required(ErrorMessage = "Mật khẩu hiện tại là bắt buộc.")]
    string? CurrentPassword,

    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc.")]
    [MinLength(8, ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự.")]
    string? NewPassword,

    [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc.")]
    [PasswordConfirmation(nameof(NewPassword))]
    string? ConfirmPassword);
