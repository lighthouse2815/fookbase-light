namespace Fookbase.Api.Modules.Events.Entities;
public enum EventParticipantStatus { Going, Interested }
public sealed class EventParticipant
{
    private EventParticipant() { }
    private EventParticipant(Guid eventId, Guid userId, EventParticipantStatus status, DateTimeOffset now) { EventId = eventId; UserId = userId; Status = status; RespondedAtUtc = now; }
    public Guid EventId { get; private set; } public Guid UserId { get; private set; } public EventParticipantStatus Status { get; private set; } public DateTimeOffset RespondedAtUtc { get; private set; }
    public static EventParticipant Create(Guid eventId, Guid userId, EventParticipantStatus status, DateTimeOffset now) => new(eventId, userId, status, now);
    public void SetStatus(EventParticipantStatus status, DateTimeOffset now) { Status = status; RespondedAtUtc = now; }
}
