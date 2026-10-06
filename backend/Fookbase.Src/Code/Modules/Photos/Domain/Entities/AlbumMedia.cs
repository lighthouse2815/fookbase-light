using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Photos.Entities;

[PrimaryKey(nameof(AlbumId), nameof(MediaId))]
[Index(nameof(AlbumId), nameof(SortOrder), nameof(MediaId))]
[Index(nameof(MediaId))]
public sealed class AlbumMedia
{
    public const int MaximumCaptionLength = 1_000;

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
    public Guid MediaId { get; private set; }
    [MaxLength(MaximumCaptionLength)]
    public string? Caption { get; private set; }
    public long SortOrder { get; private set; }
    public DateTimeOffset AddedAtUtc { get; private set; }

    public void UpdateCaption(string? caption)
    {
        Caption = NormalizeCaption(caption);
    }

    private static string? NormalizeCaption(string? value)
    {
        var caption = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (caption?.Length > MaximumCaptionLength)
        {
            throw new ArgumentException($"Photo caption cannot exceed {MaximumCaptionLength} characters.", nameof(value));
        }

        return caption;
    }
}
