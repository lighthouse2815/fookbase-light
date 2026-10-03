using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record ResetPasswordRequest(
    [Required(ErrorMessage = "Email hoặc số điện thoại là bắt buộc.")]
    [EmailOrPhoneNumber]
    string? Identifier,
    string? Token,
    string? Password,
    string? ConfirmPassword)
{
    public string? Code { get; init; }
}
