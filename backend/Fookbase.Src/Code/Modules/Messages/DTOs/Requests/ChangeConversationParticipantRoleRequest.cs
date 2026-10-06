using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Messages.Common;

namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record ChangeConversationParticipantRoleRequest(
    [Required]
    [ConversationParticipantRole(ErrorMessage = "Vai trò phải là admin hoặc member.")]
    string Role);
