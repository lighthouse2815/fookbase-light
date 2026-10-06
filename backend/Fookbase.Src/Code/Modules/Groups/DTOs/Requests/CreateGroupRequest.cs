using Fookbase.Api.Modules.Groups.Domain.Enums;
using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Identity.Common;
using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Groups.DTOs.Requests;

public sealed record CreateGroupRequest(
    [Required(ErrorMessage = "Tên nhóm là bắt buộc.")]
    [TrimmedStringLength(120)]
    string Name,

    [TrimmedStringLength(Group.MaximumDescriptionLength)]
    string? Description,

    [Required(ErrorMessage = "Quyền riêng tư của nhóm là bắt buộc.")]
    [CustomValidation(typeof(CreateGroupRequest), nameof(CreateGroupRequest.ValidatePrivacy))]
    string Privacy)
{
    public static ValidationResult? ValidatePrivacy(string? value) =>
        value is null || Enum.TryParse<GroupPrivacy>(value, true, out var privacy) && Enum.IsDefined(privacy)
            ? ValidationResult.Success
            : new ValidationResult("Quyền riêng tư phải là public hoặc private.");
}
