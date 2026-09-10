namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record SendMessageRequest(
    string? Content,
    IReadOnlyList<Guid>? MediaIds = null,
    Guid? ReplyToMessageId = null);
