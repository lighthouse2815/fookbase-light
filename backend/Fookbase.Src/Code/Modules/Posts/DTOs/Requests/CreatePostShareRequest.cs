using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Posts.DTOs.Requests;

public sealed record CreatePostShareRequest(
    [Required(ErrorMessage = "Loại đích chia sẻ là bắt buộc.")]
    [OptionalEnumValue<PostShareDestinationType>(ErrorMessage = "Đích chia sẻ phải là profile, group hoặc page.")]
    string DestinationType,

    [NonEmptyGuid(ErrorMessage = "Đích chia sẻ là bắt buộc.")]
    Guid DestinationId,

    [TrimmedStringLength(10_000, ErrorMessage = "Chú thích chia sẻ không được vượt quá {1} ký tự.")]
    string? Caption);
