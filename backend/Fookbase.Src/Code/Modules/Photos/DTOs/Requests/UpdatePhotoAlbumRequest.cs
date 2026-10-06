using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Photos.Domain.Enums;
using Fookbase.Api.Modules.Photos.Entities;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Photos.DTOs.Requests;

public sealed record UpdatePhotoAlbumRequest(
    [Required(ErrorMessage = "Tên album là bắt buộc.")]
    [TrimmedStringLength(PhotoAlbum.MaximumNameLength, ErrorMessage = "Tên album không được vượt quá {1} ký tự.")]
    string Name,

    [TrimmedStringLength(PhotoAlbum.MaximumDescriptionLength, ErrorMessage = "Mô tả album không được vượt quá {1} ký tự.")]
    string? Description,

    [Required(ErrorMessage = "Quyền riêng tư là bắt buộc.")]
    [OptionalEnumValue<PhotoAlbumPrivacy>(ErrorMessage = "Quyền riêng tư phải là public, friends hoặc only_me.")]
    string Privacy);
