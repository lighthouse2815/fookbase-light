using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Events.Domain.Enums;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Persistence.Annotations;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Events.Entities;

[Table("Events")]
[Index(nameof(Status), nameof(Privacy), nameof(StartsAtUtc))]
[IndexFilter("\"DeletedAtUtc\" IS NULL", nameof(Status), nameof(Privacy), nameof(StartsAtUtc))]
[Index(nameof(HostType), nameof(HostId), nameof(StartsAtUtc))]
[IndexFilter("\"DeletedAtUtc\" IS NULL", nameof(HostType), nameof(HostId), nameof(StartsAtUtc))]
public sealed class Event
{
    public const int MaximumNameLength = 160;
    public const int MaximumDescriptionLength = 10_000;
    private Event()
    {
    }
    private Event(Guid id, string name, string? description, EventHostType hostType, Guid hostId,
        Guid createdByUserId, EventPrivacy privacy, EventLocationType locationType, string? locationName,
        string? address, string? onlineUrl, DateTimeOffset startsAtUtc, DateTimeOffset? endsAtUtc,
        EventStatus status, DateTimeOffset now)
    {
        Id = id;
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        HostType = hostType;
        HostId = hostId;
        CreatedByUserId = createdByUserId;
        Privacy = privacy;
        LocationType = locationType;
        SetLocation(locationName, address, onlineUrl);
        StartsAtUtc = startsAtUtc.ToUniversalTime();
        EndsAtUtc = NormalizeEnd(endsAtUtc, StartsAtUtc);
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
    public EventPrivacy Privacy { get; private set; }
    public EventLocationType LocationType { get; private set; }
    public string? LocationName { get; private set; }
    public string? Address { get; private set; }
    public string? OnlineUrl { get; private set; }
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset? EndsAtUtc { get; private set; }
    public Guid? CoverMediaId { get; private set; }
    public EventStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }
    [ForeignKey(nameof(CreatedByUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User CreatedByUser { get; private set; } = null!;

    [ForeignKey(nameof(CoverMediaId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset? CoverMedia { get; private set; }

    public ICollection<EventParticipant> Participants { get; private set; } = new List<EventParticipant>();
    public ICollection<EventInvitation> Invitations { get; private set; } = new List<EventInvitation>();
    public EventCoverMediaReference? CoverMediaReference { get; private set; }

    public static Event Create(Guid id, string name, string? description, EventHostType hostType, Guid hostId,
        Guid createdByUserId, EventPrivacy privacy, EventLocationType locationType, string? locationName,
        string? address, string? onlineUrl, DateTimeOffset startsAtUtc, DateTimeOffset? endsAtUtc,
        EventStatus status, DateTimeOffset now) => new(id, name, description, hostType, hostId, createdByUserId,
        privacy, locationType, locationName, address, onlineUrl, startsAtUtc, endsAtUtc, status, now);
    public void Update(string name, string? description, EventPrivacy privacy, EventLocationType locationType,
        string? locationName, string? address, string? onlineUrl, DateTimeOffset startsAtUtc, DateTimeOffset? endsAtUtc,
        DateTimeOffset now)
    {
        EnsureActive();
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        Privacy = privacy;
        LocationType = locationType;
        SetLocation(locationName, address, onlineUrl);
        StartsAtUtc = startsAtUtc.ToUniversalTime();
        EndsAtUtc = NormalizeEnd(endsAtUtc, StartsAtUtc);
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
        LocationName = Clean(locationName);
        Address = Clean(address);
        OnlineUrl = Clean(onlineUrl);
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
    private static string NormalizeName(string? value)
    {
        var text = Clean(value) ?? string.Empty;
        if (text.Length is < 1 or > MaximumNameLength)
            throw new ArgumentException($"Event name must contain 1-{MaximumNameLength} characters.");
        return text;
    }
    private static string? NormalizeDescription(string? value)
    {
        var text = Clean(value);
        if (text?.Length > MaximumDescriptionLength)
            throw new ArgumentException($"Event description cannot exceed {MaximumDescriptionLength} characters.");
        return text;
    }
    private static DateTimeOffset? NormalizeEnd(DateTimeOffset? end, DateTimeOffset start)
    {
        if (end is null)
            return null;
        var utc = end.Value.ToUniversalTime();
        if (utc <= start)
            throw new ArgumentException("Event end time must be after start time.");
        return utc;
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
