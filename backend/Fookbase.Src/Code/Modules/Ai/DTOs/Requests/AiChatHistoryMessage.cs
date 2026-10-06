using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Ai.Common;

namespace Fookbase.Api.Modules.Ai.DTOs.Requests;

public sealed record AiChatHistoryMessage(
    [property: Required(ErrorMessage = "Vai trò trong lịch sử trò chuyện là bắt buộc.")]
    [property: AllowedValues("user", "assistant",
        ErrorMessage = "Vai trò trong lịch sử trò chuyện phải là user hoặc assistant.")]
    string? Role,

    [property: Required(ErrorMessage = "Nội dung tin nhắn là bắt buộc.")]
    [property: AiInputLength]
    string? Content);
