using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Ai.Common;

namespace Fookbase.Api.Modules.Ai.DTOs.Requests;

public sealed record AiChatHistoryMessage(
    [property: Required]
    [property: AllowedValues("user", "assistant")]
    string? Role,

    [property: Required]
    [property: AiInputLength]
    string? Content);
