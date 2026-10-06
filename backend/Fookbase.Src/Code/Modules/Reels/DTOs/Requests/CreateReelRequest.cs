using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Reels.DTOs.Requests;

public sealed record CreateReelRequest(
    [TrimmedStringLength(10_000, ErrorMessage = "Chú thích reel không được vượt quá {1} ký tự.")]
    string? Caption,

    [Required(ErrorMessage = "Quyền riêng tư là bắt buộc.")]
    [OptionalEnumValue<PostPrivacy>(ErrorMessage = "Quyền riêng tư phải là public, friends hoặc onlyMe.")]
    string Privacy,

    [NonEmptyGuid(ErrorMessage = "Video reel là bắt buộc.")]
    Guid VideoMediaId);
