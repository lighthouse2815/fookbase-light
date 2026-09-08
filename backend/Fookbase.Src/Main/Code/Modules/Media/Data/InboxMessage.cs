namespace Fookbase.Api.Modules.Media.Data;

public sealed class InboxMessage
{
    private InboxMessage() { }
    private InboxMessage(Guid eventId, string eventType, DateTimeOffset processedAtUtc)
    {
        EventId = eventId; EventType = eventType; ProcessedAtUtc = processedAtUtc;
    }

    public Guid EventId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public DateTimeOffset ProcessedAtUtc { get; private set; }
    public static InboxMessage Create(Guid id, string type, DateTimeOffset at) => new(id, type, at);
}
