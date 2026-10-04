using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record TwoFactorCodeRequest(
    [Required(ErrorMessage = "Mã xác thực hai bước là bắt buộc.")]
    string? Code);
