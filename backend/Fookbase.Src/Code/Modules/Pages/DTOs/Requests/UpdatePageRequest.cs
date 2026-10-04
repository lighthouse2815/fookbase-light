using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Pages.Entities;

namespace Fookbase.Api.Modules.Pages.DTOs.Requests;

public sealed record UpdatePageRequest(
    [Required(ErrorMessage = "Tên trang là bắt buộc.")]
    [TrimmedStringLength(Page.MaximumNameLength, ErrorMessage = "Tên trang không được vượt quá {1} ký tự.")]
    string Name,

    [Required(ErrorMessage = "Tên người dùng của trang là bắt buộc.")]
    [TrimmedStringLength(Page.MaximumUsernameLength, MinimumLength = Page.MinimumUsernameLength,
        ErrorMessage = "Tên người dùng của trang phải có từ {2} đến {1} ký tự.")]
    string Username,

    [Required(ErrorMessage = "Danh mục trang là bắt buộc.")]
    [TrimmedStringLength(Page.MaximumCategoryLength, ErrorMessage = "Danh mục trang không được vượt quá {1} ký tự.")]
    string Category,

    [TrimmedStringLength(Page.MaximumBioLength, ErrorMessage = "Giới thiệu trang không được vượt quá {1} ký tự.")]
    string? Bio);
