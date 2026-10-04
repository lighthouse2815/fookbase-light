using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record GoogleLinkRequest(
    [Required(ErrorMessage = "Mã xác thực Google là bắt buộc.")]
    string? Code,

    [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
    string? Password,

    [Required(ErrorMessage = "Ứng dụng xác thực Google là bắt buộc.")]
    string? Client);
