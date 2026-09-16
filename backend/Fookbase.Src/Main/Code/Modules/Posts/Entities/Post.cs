namespace Fookbase.Api.Modules.Posts.Entities;

public enum PostPrivacy
{
    Public,
    Friends,
    OnlyMe
}

public enum PostContainerType
{
    Profile,
    Group,
    Page,
    Event
}

public enum PostType
{
    Standard,
    Reel
}

public sealed class Post
{
    public const int MaximumContentLength = 10_000;
    public const string AvatarUpdatedPostContent = "đã cập nhật ảnh đại diện.";
    public const string CoverUpdatedPostContent = "đã cập nhật ảnh bìa.";

    private Post()
    {
    }

    private Post(
        Guid id,
        Guid authorUserId,
        string content,
        PostPrivacy privacy,
        PostContainerType containerType,
        Guid containerId,
        PostType postType,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        AuthorUserId = authorUserId;
        Content = NormalizeContent(content);
        Privacy = privacy;
        ContainerType = containerType;
        ContainerId = containerId;
        PostType = postType;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public PostPrivacy Privacy { get; private set; }

    public PostContainerType ContainerType { get; private set; }

    public Guid ContainerId { get; private set; }

    public PostType PostType { get; private set; }

    public bool IsPinned { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public static Post Create(
        Guid id,
        Guid authorUserId,
        string content,
        PostPrivacy privacy,
        DateTimeOffset createdAtUtc) =>
        new(id, authorUserId, content, privacy, PostContainerType.Profile, authorUserId,
            PostType.Standard, createdAtUtc);

    public static Post CreateInContainer(
        Guid id,
        Guid authorUserId,
        string content,
        PostPrivacy privacy,
        PostContainerType containerType,
        Guid containerId,
        DateTimeOffset createdAtUtc,
        PostType postType = PostType.Standard) =>
        new(id, authorUserId, content, privacy, containerType, containerId, postType, createdAtUtc);

    public static Post CreateReel(
        Guid id,
        Guid authorUserId,
        string caption,
        PostPrivacy privacy,
        DateTimeOffset createdAtUtc) =>
        new(id, authorUserId, caption, privacy, PostContainerType.Profile, authorUserId,
            PostType.Reel, createdAtUtc);

    public void Update(string content, PostPrivacy privacy, DateTimeOffset updatedAtUtc)
    {
        EnsureActive();
        Content = NormalizeContent(content);
        Privacy = privacy;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Delete(DateTimeOffset deletedAtUtc)
    {
        EnsureActive();
        DeletedAtUtc = deletedAtUtc;
    }

    public static bool IsProfileMediaUpdateContent(string content) =>
        content is AvatarUpdatedPostContent or CoverUpdatedPostContent;

    public void SetPinned(bool isPinned)
    {
        EnsureActive();
        IsPinned = isPinned;
    }

    private void EnsureActive()
    {
        if (DeletedAtUtc is not null)
        {
            throw new InvalidOperationException("A deleted post cannot be changed.");
        }
    }

    private static string NormalizeContent(string content)
    {
        var normalized = content.Trim();
        if (normalized.Length > MaximumContentLength)
        {
            throw new ArgumentException(
                $"Post content cannot exceed {MaximumContentLength} characters.");
        }

        return normalized;
    }
}
