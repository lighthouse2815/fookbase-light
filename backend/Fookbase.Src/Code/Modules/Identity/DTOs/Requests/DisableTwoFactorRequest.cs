using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record DisableTwoFactorRequest(
    [Required(ErrorMessage = "Mật khẩu hiện tại là bắt buộc.")]
    string? CurrentPassword);
