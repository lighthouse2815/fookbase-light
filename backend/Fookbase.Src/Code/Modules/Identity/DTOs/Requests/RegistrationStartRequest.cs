using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record RegistrationStartRequest(

    [Required(ErrorMessage = "Tên là bắt buộc.")]
    [TrimmedStringLength(50, ErrorMessage = "Tên không được vượt quá 50 ký tự.")]
    string? FirstName,

    [Required(ErrorMessage = "Họ là bắt buộc.")]
    [TrimmedStringLength(50, ErrorMessage = "Họ không được vượt quá 50 ký tự.")]
    string? LastName,

    [Required(ErrorMessage = "Ngày sinh là bắt buộc.")]
    DateOnly? DateOfBirth,

    [Required(ErrorMessage = "Giới tính là bắt buộc.")]
    string? Gender,

    [Required(ErrorMessage = "Email hoặc số điện thoại là bắt buộc.")]
    [EmailOrPhoneNumber]
    string? Contact,

    [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
    [MinLength(8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
    string? Password);
