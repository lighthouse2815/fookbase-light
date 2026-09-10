namespace Fookbase.Api.Modules.Messages.DTOs.Responses;

public sealed record MessageHistoryResponse(
    IReadOnlyList<MessageResponse> Items,
    string? NextCursor,
    bool HasMore);
