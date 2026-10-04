using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record GoogleMobileCompletionRequest(

    [Required(ErrorMessage = "Mã xác thực Google là bắt buộc.")]
    string? Code,

    [Required(ErrorMessage = "Mã xác minh PKCE là bắt buộc.")]
    string? Verifier,

    string? Password = null,

    string? Client = null);
