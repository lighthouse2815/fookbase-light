using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record RegisterRequest(
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    string? Email,

    [Required(ErrorMessage = "Tên đăng nhập là bắt buộc.")]
    [TrimmedStringLength(32, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải có từ 3 đến 32 ký tự.")]
    string? Username,

    [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Mật khẩu phải có từ 8 đến 128 ký tự.")]
    string? Password);
