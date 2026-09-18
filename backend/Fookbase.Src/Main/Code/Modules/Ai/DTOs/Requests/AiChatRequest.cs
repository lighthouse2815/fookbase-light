namespace Fookbase.Api.Modules.Ai.DTOs.Requests;

public sealed record AiChatRequest(string? Message, IReadOnlyList<AiChatHistoryMessage>? History);

public sealed record AiChatHistoryMessage(string? Role, string? Content);
