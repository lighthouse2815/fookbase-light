using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Events.Domain.Enums;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Events.Entities;

[PrimaryKey(nameof(EventId), nameof(UserId))]
[Index(nameof(UserId), nameof(Status))]
public sealed class EventParticipant
{
    private EventParticipant() { }

    private EventParticipant(
        Guid eventId,
        Guid userId,
        EventParticipantStatus status,
        DateTimeOffset now)
    {
        EventId = eventId;
        UserId = userId;
        Status = status;
        RespondedAtUtc = now;
    }

    public Guid EventId { get; private set; }

    [ForeignKey(nameof(EventId))]
    [InverseProperty(nameof(Event.Participants))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Event Event { get; private set; } = null!;

    public Guid UserId { get; private set; }

    [ForeignKey(nameof(UserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User User { get; private set; } = null!;

    public EventParticipantStatus Status { get; private set; }

    public DateTimeOffset RespondedAtUtc { get; private set; }

    public static EventParticipant Create(Guid eventId, Guid userId, EventParticipantStatus status, DateTimeOffset now) =>
        new(eventId, userId, status, now);

    public void SetStatus(EventParticipantStatus status, DateTimeOffset now)
    {
        Status = status;
        RespondedAtUtc = now;
    }
}
