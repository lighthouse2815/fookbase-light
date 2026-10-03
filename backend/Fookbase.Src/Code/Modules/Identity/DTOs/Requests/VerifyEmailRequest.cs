using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record VerifyEmailRequest(
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    string? Email,

    [Required(ErrorMessage = "Token xác minh email là bắt buộc.")]
    string? Token);
