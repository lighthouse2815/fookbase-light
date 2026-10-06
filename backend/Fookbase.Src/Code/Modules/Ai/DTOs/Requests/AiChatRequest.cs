using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Ai.Common;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Fookbase.Api.Modules.Ai.DTOs.Requests;

public sealed record AiChatRequest(
    [Required(ErrorMessage = "Nội dung tin nhắn là bắt buộc.")]
    [AiInputLength]
    string? Message,

    // History is filtered by the service so invalid entries do not reject the current message.
    [ValidateNever]
    IReadOnlyList<AiChatHistoryMessage>? History);
