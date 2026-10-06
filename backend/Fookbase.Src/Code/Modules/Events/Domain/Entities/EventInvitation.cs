using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Events.Domain.Enums;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Persistence.Annotations;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Events.Entities;

[Index(nameof(EventId), nameof(InviteeUserId))]
[IndexFilter("\"Status\" = 0", nameof(EventId), nameof(InviteeUserId))]
[Index(nameof(InviteeUserId), nameof(Status), nameof(CreatedAtUtc))]
public sealed class EventInvitation
{
    private EventInvitation() { }

    public EventInvitation(
        Guid id,
        Guid eventId,
        Guid inviterUserId,
        Guid inviteeUserId,
        DateTimeOffset now)
    {
        Id = id;
        EventId = eventId;
        InviterUserId = inviterUserId;
        InviteeUserId = inviteeUserId;
        Status = EventInvitationStatus.PENDING;
        CreatedAtUtc = now;
    }

    [Key]
    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    [InverseProperty(nameof(Event.Invitations))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Event Event { get; private set; } = null!;

    public Guid InviterUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User InviterUser { get; private set; } = null!;

    public Guid InviteeUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User InviteeUser { get; private set; } = null!;

    public EventInvitationStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? RespondedAtUtc { get; private set; }

    public void Accept(DateTimeOffset now)
    {
        if (Status == EventInvitationStatus.PENDING)
        {
            Status = EventInvitationStatus.ACCEPTED;
            RespondedAtUtc = now;
        }
    }

    public void Decline(DateTimeOffset now)
    {
        if (Status == EventInvitationStatus.PENDING)
        {
            Status = EventInvitationStatus.DECLINED;
            RespondedAtUtc = now;
        }
    }
}
