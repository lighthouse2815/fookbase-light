using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Events.Entities;

[Index(nameof(MediaId))]
public sealed class EventCoverMediaReference
{
    private EventCoverMediaReference() { }

    public EventCoverMediaReference(
        Guid eventId,
        Guid mediaId,
        DateTimeOffset now)
    {
        EventId = eventId;
        MediaId = mediaId;
        AttachedAtUtc = now;
    }

    [Key]
    public Guid EventId { get; private set; }

    [InverseProperty(nameof(Event.CoverMediaReference))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Event Event { get; private set; } = null!;

    public Guid MediaId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset Media { get; private set; } = null!;

    public DateTimeOffset AttachedAtUtc { get; private set; }
}
