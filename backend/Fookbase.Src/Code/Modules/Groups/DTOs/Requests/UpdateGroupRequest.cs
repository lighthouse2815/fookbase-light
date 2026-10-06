using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Groups.Domain.Enums;
using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Groups.DTOs.Requests;

public sealed record UpdateGroupRequest(
    [Required(ErrorMessage = "Tên nhóm là bắt buộc.")]
    [TrimmedStringLength(120)]
    string Name,

    [TrimmedStringLength(Group.MaximumDescriptionLength)]
    string? Description,

    [Required(ErrorMessage = "Quyền riêng tư của nhóm là bắt buộc.")]
    [OptionalEnumValue<GroupPrivacy>(ErrorMessage = "Quyền riêng tư phải là public hoặc private.")]
    string Privacy,
    Guid? CoverMediaId,
    bool RemoveCover = false);
