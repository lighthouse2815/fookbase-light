using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Stories.DTOs.Requests;

public sealed record CreateStoryRequest(
    [NonEmptyGuid(ErrorMessage = "Media là bắt buộc.")]
    Guid MediaId,

    [TrimmedStringLength(2_200,
        ErrorMessage = "Chú thích story không được vượt quá {1} ký tự.")]
    string? Caption,

    [Required(ErrorMessage = "Quyền riêng tư của story là bắt buộc.")]
    string? Privacy);
