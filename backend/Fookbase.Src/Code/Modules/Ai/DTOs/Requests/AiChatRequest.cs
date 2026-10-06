using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Ai.Common;
using Microsoft.Extensions.Validation;

namespace Fookbase.Api.Modules.Ai.DTOs.Requests;

public sealed record AiChatRequest(
    [property: Required(ErrorMessage = "Nội dung tin nhắn là bắt buộc.")]
    [property: AiInputLength]
    string? Message,

    // History is filtered by the service so invalid entries do not reject the current message.
#pragma warning disable ASP0029 // .NET 10 marks SkipValidation as experimental.
    [SkipValidation]
#pragma warning restore ASP0029
    IReadOnlyList<AiChatHistoryMessage>? History);
