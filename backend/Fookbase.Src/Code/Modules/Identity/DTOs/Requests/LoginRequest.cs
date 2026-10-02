using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record LoginRequest(
    [property: Required(ErrorMessage = "Email hoặc số điện thoại là bắt buộc.")]
    string? Identifier,
    [property: Required(ErrorMessage = "Password is required.")]
    string? Password);
