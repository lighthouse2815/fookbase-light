using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Photos.Entities;

[PrimaryKey(nameof(AlbumId), nameof(MediaId))]
[Index(nameof(AlbumId), nameof(SortOrder), nameof(MediaId))]
[Index(nameof(MediaId))]
public sealed class AlbumMedia
{
    private AlbumMedia()
    {
    }

    public AlbumMedia(Guid albumId, Guid mediaId, long sortOrder, DateTimeOffset addedAtUtc)
    {
        AlbumId = albumId;
        MediaId = mediaId;
        SortOrder = sortOrder;
        AddedAtUtc = addedAtUtc;
    }

    public Guid AlbumId { get; private set; }

    [InverseProperty(nameof(PhotoAlbum.MediaItems))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public PhotoAlbum Album { get; private set; } = null!;

    public Guid MediaId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset Media { get; private set; } = null!;

    [MaxLength(1_000)]
    public string? Caption { get; private set; }
    public long SortOrder { get; private set; }
    public DateTimeOffset AddedAtUtc { get; private set; }

    public void UpdateCaption(string? caption)
    {
        Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
    }
}
