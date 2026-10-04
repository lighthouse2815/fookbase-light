using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Messages.Common;
using Fookbase.Api.Modules.Messages.Services;

namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record AddConversationParticipantsRequest(
    [Required]
    [MinLength(1)]
    [MaxLength(MessagesService.MaximumGroupSize)]
    [CustomValidation(typeof(MessageRequestValidation), nameof(MessageRequestValidation.ValidateDistinctIds))]
    IReadOnlyList<Guid> UserIds);
