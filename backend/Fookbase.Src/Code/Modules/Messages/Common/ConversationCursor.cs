namespace Fookbase.Api.Modules.Messages.Common;

internal sealed record ConversationCursor(DateTimeOffset LastMessageAtUtc, Guid ConversationId);
