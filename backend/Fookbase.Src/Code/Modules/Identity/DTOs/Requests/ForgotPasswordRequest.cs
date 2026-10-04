using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record ForgotPasswordRequest(

    [Required(ErrorMessage = "Email hoặc số điện thoại là bắt buộc.")]
    [EmailOrPhoneNumber]
    string? Identifier);
