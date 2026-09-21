namespace Fookbase.Api.Modules.Photos.Entities;

public sealed class AlbumMedia
{
    public const int MaximumCaptionLength = 1_000;

    private AlbumMedia()
    {
    }

    private AlbumMedia(Guid albumId, Guid mediaId, string? caption, long sortOrder, DateTimeOffset addedAtUtc)
    {
        AlbumId = albumId;
        MediaId = mediaId;
        Caption = NormalizeCaption(caption);
        SortOrder = sortOrder;
        AddedAtUtc = addedAtUtc;
    }

    public Guid AlbumId { get; private set; }
    public Guid MediaId { get; private set; }
    public string? Caption { get; private set; }
    public long SortOrder { get; private set; }
    public DateTimeOffset AddedAtUtc { get; private set; }

    public static AlbumMedia Create(Guid albumId, Guid mediaId, long sortOrder, DateTimeOffset addedAtUtc) =>
        new(albumId, mediaId, null, sortOrder, addedAtUtc);

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
