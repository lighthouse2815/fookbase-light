namespace Fookbase.Api.Modules.Messages.DTOs.Responses;

public sealed record MessagesReadResponse(
    Guid ConversationId,
    Guid ReaderUserId,
    Guid LastReadMessageId,
    DateTimeOffset ReadAtUtc);
