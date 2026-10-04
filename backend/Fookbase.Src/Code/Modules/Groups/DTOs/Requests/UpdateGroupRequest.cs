using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Identity.Common;
using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Groups.DTOs.Requests;

public sealed record UpdateGroupRequest(
    [Required(ErrorMessage = "Tên nhóm là bắt buộc.")]
    [TrimmedStringLength(Group.MaximumNameLength)]
    string Name,

    [TrimmedStringLength(Group.MaximumDescriptionLength)]
    string? Description,

    [Required(ErrorMessage = "Quyền riêng tư của nhóm là bắt buộc.")]
    [CustomValidation(typeof(CreateGroupRequest), nameof(CreateGroupRequest.ValidatePrivacy))]
    string Privacy,
    Guid? CoverMediaId,
    bool RemoveCover = false);
