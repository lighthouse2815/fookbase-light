using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Pages.DTOs.Requests;

public sealed record CreatePageRequest(
    [Required(ErrorMessage = "Tên trang là bắt buộc.")]
    [TrimmedStringLength(120, ErrorMessage = "Tên trang không được vượt quá {1} ký tự.")]
    string Name,

    [Required(ErrorMessage = "Tên người dùng của trang là bắt buộc.")]
    [TrimmedStringLength(50, MinimumLength = 3,
        ErrorMessage = "Tên người dùng của trang phải có từ {2} đến {1} ký tự.")]
    string Username,

    [Required(ErrorMessage = "Danh mục trang là bắt buộc.")]
    [TrimmedStringLength(80, ErrorMessage = "Danh mục trang không được vượt quá {1} ký tự.")]
    string Category,

    [TrimmedStringLength(2_000, ErrorMessage = "Giới thiệu trang không được vượt quá {1} ký tự.")]
    string? Bio);
