using Fookbase.Api.Modules.Groups.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Groups.DTOs.Requests;

public sealed record ChangeGroupMemberRoleRequest(
    [Required(ErrorMessage = "Vai trò trong nhóm là bắt buộc.")]
    [CustomValidation(typeof(ChangeGroupMemberRoleRequest), nameof(ChangeGroupMemberRoleRequest.ValidateRole))]
    string Role)
{
    public static ValidationResult? ValidateRole(string? value) =>
        value is null || Enum.TryParse<GroupMemberRole>(value, true, out var role) && Enum.IsDefined(role)
            ? ValidationResult.Success
            : new ValidationResult("Vai trò phải là owner, admin, moderator hoặc member.");
}
