namespace Fookbase.Api.Modules.Events.Entities;
public sealed class EventCoverMediaReference
{ private EventCoverMediaReference() { } private EventCoverMediaReference(Guid eventId, Guid mediaId, DateTimeOffset now) { EventId=eventId; MediaId=mediaId; AttachedAtUtc=now; }
  public Guid EventId { get; private set; } public Guid MediaId { get; private set; } public DateTimeOffset AttachedAtUtc { get; private set; }
  public static EventCoverMediaReference Create(Guid eventId, Guid mediaId, DateTimeOffset now) => new(eventId,mediaId,now); }
