using Fookbase.Api.Modules.Posts.Entities;

namespace Fookbase.Api.Modules.Stories.Entities;

public sealed class Story
{
    public const int MaximumCaptionLength = 2_200;

    private Story() { }

    private Story(
        Guid id,
        Guid authorUserId,
        Guid mediaId,
        string? caption,
        PostPrivacy privacy,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        Id = id;
        AuthorUserId = authorUserId;
        MediaId = mediaId;
        Caption = NormalizeCaption(caption);
        Privacy = privacy;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public Guid MediaId { get; private set; }
    public string? Caption { get; private set; }
    public PostPrivacy Privacy { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public static Story Create(
        Guid id,
        Guid authorUserId,
        Guid mediaId,
        string? caption,
        PostPrivacy privacy,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc) =>
        new(id, authorUserId, mediaId, caption, privacy, createdAtUtc, expiresAtUtc);

    public void Delete(DateTimeOffset deletedAtUtc)
    {
        if (DeletedAtUtc is null)
        {
            DeletedAtUtc = deletedAtUtc;
        }
    }

    private static string? NormalizeCaption(string? caption)
    {
        var normalized = caption?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > MaximumCaptionLength)
        {
            throw new ArgumentException($"Story caption cannot exceed {MaximumCaptionLength} characters.");
        }

        return normalized;
    }
}
