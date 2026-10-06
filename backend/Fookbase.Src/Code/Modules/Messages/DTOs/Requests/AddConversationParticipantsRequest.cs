using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Messages.Common;
using Fookbase.Api.Modules.Messages.Services;

namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record AddConversationParticipantsRequest(
    [Required]
    [MinLength(1)]
    [MaxLength(MessagesService.MaximumGroupSize)]
    [DistinctMessageIds(ErrorMessage = "Danh sách ID phải khác rỗng và không được trùng nhau.")]
    IReadOnlyList<Guid> UserIds);
