namespace Fookbase.Api.Modules.Friends.Repositories;

public sealed class InboxMessage
{
    private InboxMessage()
    {
    }

    private InboxMessage(Guid eventId, string eventType, DateTimeOffset processedAtUtc)
    {
        EventId = eventId;
        EventType = eventType;
        ProcessedAtUtc = processedAtUtc;
    }

    public Guid EventId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public DateTimeOffset ProcessedAtUtc { get; private set; }

    public static InboxMessage Create(
        Guid eventId,
        string eventType,
        DateTimeOffset processedAtUtc) =>
        new(eventId, eventType, processedAtUtc);
}
