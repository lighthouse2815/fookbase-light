using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Groups.Domain.Enums;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Groups.DTOs.Requests;

public sealed record ChangeGroupMemberRoleRequest(
    [Required(ErrorMessage = "Vai trò trong nhóm là bắt buộc.")]
    [OptionalEnumValue<GroupMemberRole>(ErrorMessage = "Vai trò phải là owner, admin, moderator hoặc member.")]
    string Role);
