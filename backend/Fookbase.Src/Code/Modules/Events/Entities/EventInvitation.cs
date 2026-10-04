namespace Fookbase.Api.Modules.Events.Entities;
public enum EventInvitationStatus { PENDING, ACCEPTED, DECLINED }
public sealed class EventInvitation
{
    private EventInvitation() { }
    private EventInvitation(Guid id, Guid eventId, Guid inviterUserId, Guid inviteeUserId, DateTimeOffset now) { Id=id; EventId=eventId; InviterUserId=inviterUserId; InviteeUserId=inviteeUserId; Status=EventInvitationStatus.PENDING; CreatedAtUtc=now; }
    public Guid Id { get; private set; } public Guid EventId { get; private set; } public Guid InviterUserId { get; private set; } public Guid InviteeUserId { get; private set; } public EventInvitationStatus Status { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; } public DateTimeOffset? RespondedAtUtc { get; private set; }
    public static EventInvitation Create(Guid id, Guid eventId, Guid inviterUserId, Guid inviteeUserId, DateTimeOffset now) => new(id,eventId,inviterUserId,inviteeUserId,now);
    public void Accept(DateTimeOffset now) { if (Status == EventInvitationStatus.PENDING) { Status=EventInvitationStatus.ACCEPTED; RespondedAtUtc=now; } }
    public void Decline(DateTimeOffset now) { if (Status == EventInvitationStatus.PENDING) { Status=EventInvitationStatus.DECLINED; RespondedAtUtc=now; } }
}
