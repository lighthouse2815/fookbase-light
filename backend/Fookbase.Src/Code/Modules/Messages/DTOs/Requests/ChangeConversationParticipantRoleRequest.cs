using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Messages.Domain.Enums;

namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record ChangeConversationParticipantRoleRequest(
    [Required]
    [CustomValidation(typeof(ChangeConversationParticipantRoleRequest), nameof(ChangeConversationParticipantRoleRequest.ValidateRole))]
    string Role)
{
    public static ValidationResult? ValidateRole(string? value) =>
        value is null || Enum.TryParse<ConversationParticipantRole>(value, true, out var role) &&
            role is ConversationParticipantRole.ADMIN or ConversationParticipantRole.MEMBER
            ? ValidationResult.Success
            : new ValidationResult("Vai trò phải là admin hoặc member.");
}
