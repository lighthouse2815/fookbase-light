using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Users.Common;
using Fookbase.Api.Modules.Users.Domain.Enums;

namespace Fookbase.Api.Modules.Users.DTOs.Requests;

public sealed record UpdateUserProfileRequest(

    [TrimmedStringLength(100, MinimumLength = 1,
        ErrorMessage = "Tên hiển thị phải có từ 1 đến 100 ký tự.")]
    string? DisplayName,

    [StringLength(500, ErrorMessage = "Giới thiệu không được vượt quá 500 ký tự.")]
    string? Bio,

    DateOnly? DateOfBirth,

    [StringLength(100, ErrorMessage = "Thành phố hiện tại không được vượt quá 100 ký tự.")]
    string? CurrentCity,

    Guid? AvatarMediaId = null,

    Guid? CoverMediaId = null,

    [EnumDataType(typeof(BirthdayVisibility), ErrorMessage = "Quyền hiển thị ngày sinh không hợp lệ.")]
    BirthdayVisibility? BirthdayVisibility = null,

    [StringLength(100, ErrorMessage = "Quê quán không được vượt quá 100 ký tự.")]
    string? Hometown = null,

    [StringLength(150, ErrorMessage = "Nơi làm việc không được vượt quá 150 ký tự.")]
    string? Workplace = null,

    [StringLength(150, ErrorMessage = "Học vấn không được vượt quá 150 ký tự.")]
    string? Education = null,

    [StringLength(2048, ErrorMessage = "Website không được vượt quá 2048 ký tự.")]
    [OptionalHttpUrl(ErrorMessage = "Website phải là địa chỉ HTTP hoặc HTTPS hợp lệ.")]
    string? Website = null);
