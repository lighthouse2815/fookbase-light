namespace Fookbase.Api.Modules.Posts.Entities;

public enum PostShareDestinationType
{
    Profile,
    Group,
    Page
}

public sealed class PostShare
{
    public const int MaximumCaptionLength = Post.MaximumContentLength;

    private PostShare()
    {
    }

    private PostShare(
        Guid id,
        Guid originalPostId,
        Guid sharingUserId,
        PostShareDestinationType destinationType,
        Guid destinationId,
        string? caption,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        OriginalPostId = originalPostId;
        SharingUserId = sharingUserId;
        DestinationType = destinationType;
        DestinationId = destinationId;
        Caption = NormalizeCaption(caption);
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid OriginalPostId { get; private set; }

    public Guid SharingUserId { get; private set; }

    public PostShareDestinationType DestinationType { get; private set; }

    public Guid DestinationId { get; private set; }

    public string? Caption { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public static PostShare Create(
        Guid id,
        Guid originalPostId,
        Guid sharingUserId,
        PostShareDestinationType destinationType,
        Guid destinationId,
        string? caption,
        DateTimeOffset createdAtUtc) =>
        new(id, originalPostId, sharingUserId, destinationType, destinationId, caption, createdAtUtc);

    public void Delete(DateTimeOffset deletedAtUtc)
    {
        if (DeletedAtUtc is not null)
        {
            return;
        }

        DeletedAtUtc = deletedAtUtc;
    }

    private static string? NormalizeCaption(string? caption)
    {
        if (string.IsNullOrWhiteSpace(caption))
        {
            return null;
        }

        var normalized = caption.Trim();
        if (normalized.Length > MaximumCaptionLength)
        {
            throw new ArgumentException($"Share caption cannot exceed {MaximumCaptionLength} characters.");
        }

        return normalized;
    }
}
