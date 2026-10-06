using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Photos.Domain.Enums;
using Fookbase.Api.Persistence.Annotations;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Photos.Entities;

[Index(nameof(OwnerUserId), nameof(AlbumType), IsUnique = true)]
[IndexFilter("\"DeletedAtUtc\" IS NULL AND \"AlbumType\" <> 0", nameof(OwnerUserId), nameof(AlbumType))]
[Index(nameof(OwnerUserId), nameof(CreatedAtUtc), nameof(Id))]
[IndexFilter("\"DeletedAtUtc\" IS NULL", nameof(OwnerUserId), nameof(CreatedAtUtc), nameof(Id))]
public sealed class PhotoAlbum
{
    public const int MaximumNameLength = 160;
    public const int MaximumDescriptionLength = 2_000;

    private PhotoAlbum()
    {
    }

    private PhotoAlbum(
        Guid id,
        Guid ownerUserId,
        string name,
        string? description,
        PhotoAlbumPrivacy privacy,
        PhotoAlbumType albumType,
        DateTimeOffset now)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Privacy = privacy;
        AlbumType = albumType;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    [Key]
    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User OwnerUser { get; private set; } = null!;

    [Required]
    [MaxLength(MaximumNameLength)]
    public string Name { get; private set; } = string.Empty;
    [MaxLength(MaximumDescriptionLength)]
    public string? Description { get; private set; }
    public PhotoAlbumPrivacy Privacy { get; private set; }
    public PhotoAlbumType AlbumType { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public ICollection<AlbumMedia> MediaItems { get; private set; } = new List<AlbumMedia>();

    public PhotoAlbum(
        Guid id,
        Guid ownerUserId,
        string name,
        string? description,
        PhotoAlbumPrivacy privacy,
        DateTimeOffset now)
        : this(id, ownerUserId, name, description, privacy, PhotoAlbumType.CUSTOM, now)
    {
    }

    public PhotoAlbum(
        Guid id,
        Guid ownerUserId,
        PhotoAlbumType albumType,
        DateTimeOffset now)
        : this(id, ownerUserId, SystemName(albumType), null, PhotoAlbumPrivacy.PUBLIC, albumType, now)
    {
    }

    public void UpdateCustom(string name, string? description, PhotoAlbumPrivacy privacy, DateTimeOffset now)
    {
        EnsureActiveCustom();
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Privacy = privacy;
        UpdatedAtUtc = now;
    }

    public void DeleteCustom(DateTimeOffset now)
    {
        EnsureActiveCustom();
        DeletedAtUtc = now;
        UpdatedAtUtc = now;
    }

    private void EnsureActiveCustom()
    {
        if (DeletedAtUtc is not null)
        {
            throw new InvalidOperationException("A deleted album cannot be changed.");
        }

        if (AlbumType != PhotoAlbumType.CUSTOM)
        {
            throw new InvalidOperationException("System albums cannot be changed this way.");
        }
    }

    private static string SystemName(PhotoAlbumType albumType) => albumType switch
    {
        PhotoAlbumType.CUSTOM => throw new ArgumentException("Custom albums must be created explicitly.", nameof(albumType)),
        PhotoAlbumType.PROFILE_PICTURES => "Profile pictures",
        PhotoAlbumType.COVER_PHOTOS => "Cover photos",
        PhotoAlbumType.TIMELINE_PHOTOS => "Timeline photos",
        _ => throw new ArgumentOutOfRangeException(nameof(albumType), albumType, null)
    };
}
