using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record LoginRequest(
    [property: Required(ErrorMessage = "Email hoặc số điện thoại là bắt buộc.")]
    [property: EmailOrPhoneNumber]
    string? Identifier,
    [property: Required(ErrorMessage = "Password is required.")]
    string? Password);
