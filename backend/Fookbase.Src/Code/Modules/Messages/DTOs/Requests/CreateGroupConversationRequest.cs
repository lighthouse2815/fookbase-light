using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Messages.Common;
using Fookbase.Api.Modules.Messages.Entities;
using Fookbase.Api.Modules.Messages.Services;

namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record CreateGroupConversationRequest(
    [Required]
    [TrimmedStringLength(Conversation.MaximumTitleLength, MinimumLength = 1)]
    string Title,

    [Required]
    [MinLength(1)]
    [MaxLength(MessagesService.MaximumGroupSize - 1)]
    [DistinctMessageIds(ErrorMessage = "Danh sách ID phải khác rỗng và không được trùng nhau.")]
    IReadOnlyList<Guid> ParticipantUserIds,

    Guid? PhotoMediaId = null);
