namespace Fookbase.Api.Modules.Photos.Entities;

public enum PhotoAlbumType
{
    Custom,
    ProfilePictures,
    CoverPhotos,
    TimelinePhotos
}

public enum PhotoAlbumPrivacy
{
    Public,
    Friends,
    OnlyMe
}

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
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        Privacy = privacy;
        AlbumType = albumType;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public PhotoAlbumPrivacy Privacy { get; private set; }
    public PhotoAlbumType AlbumType { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public static PhotoAlbum CreateCustom(
        Guid id,
        Guid ownerUserId,
        string name,
        string? description,
        PhotoAlbumPrivacy privacy,
        DateTimeOffset now) =>
        new(id, ownerUserId, name, description, privacy, PhotoAlbumType.Custom, now);

    public static PhotoAlbum CreateSystem(
        Guid id,
        Guid ownerUserId,
        PhotoAlbumType albumType,
        DateTimeOffset now)
    {
        if (albumType == PhotoAlbumType.Custom)
        {
            throw new ArgumentException("Custom albums must be created explicitly.", nameof(albumType));
        }

        return new PhotoAlbum(id, ownerUserId, SystemName(albumType), null, PhotoAlbumPrivacy.Public, albumType, now);
    }

    public void UpdateCustom(string name, string? description, PhotoAlbumPrivacy privacy, DateTimeOffset now)
    {
        EnsureActiveCustom();
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
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

        if (AlbumType != PhotoAlbumType.Custom)
        {
            throw new InvalidOperationException("System albums cannot be changed this way.");
        }
    }

    private static string SystemName(PhotoAlbumType albumType) => albumType switch
    {
        PhotoAlbumType.ProfilePictures => "Profile pictures",
        PhotoAlbumType.CoverPhotos => "Cover photos",
        PhotoAlbumType.TimelinePhotos => "Timeline photos",
        _ => throw new ArgumentOutOfRangeException(nameof(albumType), albumType, null)
    };

    private static string NormalizeName(string? value)
    {
        var name = value?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > MaximumNameLength)
        {
            throw new ArgumentException($"Album name must contain 1-{MaximumNameLength} characters.", nameof(value));
        }

        return name;
    }

    private static string? NormalizeDescription(string? value)
    {
        var description = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (description?.Length > MaximumDescriptionLength)
        {
            throw new ArgumentException($"Album description cannot exceed {MaximumDescriptionLength} characters.", nameof(value));
        }

        return description;
    }
}
