using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record TwoFactorVerifyRequest(
    [Required(ErrorMessage = "Mã yêu cầu xác thực hai bước là bắt buộc.")]
    string? Challenge,

    [Required(ErrorMessage = "Mã xác thực hai bước là bắt buộc.")]
    string? Code);
