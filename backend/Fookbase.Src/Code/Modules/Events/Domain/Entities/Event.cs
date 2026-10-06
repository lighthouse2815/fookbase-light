using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Events.Common;
using Fookbase.Api.Modules.Events.Domain.Enums;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Persistence.Annotations;
using Fookbase.Api.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Events.Entities;

[Index(nameof(Status), nameof(Privacy), nameof(StartsAtUtc))]
[IndexFilter("\"DeletedAtUtc\" IS NULL", nameof(Status), nameof(Privacy), nameof(StartsAtUtc))]
[Index(nameof(HostType), nameof(HostId), nameof(StartsAtUtc))]
[IndexFilter("\"DeletedAtUtc\" IS NULL", nameof(HostType), nameof(HostId), nameof(StartsAtUtc))]
public sealed class Event
{
    public const int MaximumNameLength = 160;
    public const int MaximumDescriptionLength = 10_000;

    private Event() { }

    public Event(
        Guid id,
        string name,
        string? description,
        EventHostType hostType,
        Guid hostId,
        Guid createdByUserId,
        EventPrivacy privacy,
        EventLocationType locationType,
        string? locationName,
        string? address,
        string? onlineUrl,
        DateTimeOffset startsAtUtc,
        DateTimeOffset? endsAtUtc,
        EventStatus status,
        DateTimeOffset now)
    {
        Id = id;
        Name = EventNormalization.NormalizeName(name);
        Description = EventNormalization.NormalizeDescription(description);
        HostType = hostType;
        HostId = hostId;
        CreatedByUserId = createdByUserId;
        Privacy = privacy;
        LocationType = locationType;
        SetLocation(locationName, address, onlineUrl);
        StartsAtUtc = startsAtUtc.ToUniversalTime();
        EndsAtUtc = EventNormalization.NormalizeEnd(endsAtUtc, StartsAtUtc);
        Status = status;
        CreatedAtUtc = now;
    }

    [Key]
    public Guid Id { get; private set; }

    [Required]
    [MaxLength(MaximumNameLength)]
    public string Name { get; private set; } = string.Empty;

    [MaxLength(MaximumDescriptionLength)]
    public string? Description { get; private set; }

    public EventHostType HostType { get; private set; }

    // HostId refers to a User, Group, or Page according to HostType.
    public Guid HostId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User CreatedByUser { get; private set; } = null!;

    public EventPrivacy Privacy { get; private set; }

    public EventLocationType LocationType { get; private set; }

    public string? LocationName { get; private set; }

    public string? Address { get; private set; }

    public string? OnlineUrl { get; private set; }

    public DateTimeOffset StartsAtUtc { get; private set; }

    public DateTimeOffset? EndsAtUtc { get; private set; }

    public Guid? CoverMediaId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset? CoverMedia { get; private set; }

    public EventStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public ICollection<EventParticipant> Participants { get; private set; } = new List<EventParticipant>();

    public ICollection<EventInvitation> Invitations { get; private set; } = new List<EventInvitation>();

    public EventCoverMediaReference? CoverMediaReference { get; private set; }

    public void Update(
        string name,
        string? description,
        EventPrivacy privacy,
        EventLocationType locationType,
        string? locationName,
        string? address,
        string? onlineUrl,
        DateTimeOffset startsAtUtc,
        DateTimeOffset? endsAtUtc,
        DateTimeOffset now)
    {
        EnsureActive();
        Name = EventNormalization.NormalizeName(name);
        Description = EventNormalization.NormalizeDescription(description);
        Privacy = privacy;
        LocationType = locationType;
        SetLocation(locationName, address, onlineUrl);
        StartsAtUtc = startsAtUtc.ToUniversalTime();
        EndsAtUtc = EventNormalization.NormalizeEnd(endsAtUtc, StartsAtUtc);
        UpdatedAtUtc = now;
    }

    public void SetCover(Guid? mediaId, DateTimeOffset now)
    {
        EnsureActive();
        CoverMediaId = mediaId;
        UpdatedAtUtc = now;
    }

    public void Publish(DateTimeOffset now)
    {
        EnsureActive();
        if (Status != EventStatus.DRAFT)
            throw new InvalidOperationException("Only draft events can be published.");
        Status = EventStatus.PUBLISHED;
        UpdatedAtUtc = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        EnsureActive();
        if (Status != EventStatus.PUBLISHED)
            throw new InvalidOperationException("Only published events can be cancelled.");
        Status = EventStatus.CANCELLED;
        UpdatedAtUtc = now;
    }

    public void Delete(DateTimeOffset now)
    {
        EnsureActive();
        DeletedAtUtc = now;
        CoverMediaId = null;
        UpdatedAtUtc = now;
    }

    private void EnsureActive()
    {
        if (DeletedAtUtc is not null)
            throw new InvalidOperationException("A deleted event cannot be changed.");
    }

    private void SetLocation(string? locationName, string? address, string? onlineUrl)
    {
        LocationName = TextNormalization.NormalizeOptionalText(locationName);
        Address = TextNormalization.NormalizeOptionalText(address);
        OnlineUrl = TextNormalization.NormalizeOptionalText(onlineUrl);
        if (LocationType == EventLocationType.ONLINE)
        {
            if (!Uri.TryCreate(OnlineUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
                throw new ArgumentException("Online events require a safe http or https URL.");
            LocationName = null;
            Address = null;
        }
        else
            OnlineUrl = null;
    }
}
