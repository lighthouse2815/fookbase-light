namespace Fookbase.Api.Modules.Messages.Common;

internal sealed record MessageCursor(DateTimeOffset CreatedAtUtc, Guid MessageId);
