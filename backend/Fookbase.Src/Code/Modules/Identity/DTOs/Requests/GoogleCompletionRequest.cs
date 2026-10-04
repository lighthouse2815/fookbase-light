using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record GoogleCompletionRequest(

    [Required(ErrorMessage = "Mã xác thực Google là bắt buộc.")]
    string? Code,

    [Required(ErrorMessage = "Ứng dụng xác thực Google là bắt buộc.")]
    string? Client);
