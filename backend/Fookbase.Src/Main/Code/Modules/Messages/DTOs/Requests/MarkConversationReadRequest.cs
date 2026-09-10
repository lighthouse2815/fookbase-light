namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record MarkConversationReadRequest(Guid? LastReadMessageId);
