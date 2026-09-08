namespace Fookbase.Api.Modules.Posts.Models;

public enum PostPrivacy
{
    Public,
    Friends,
    OnlyMe
}

public sealed class Post
{
    public const int MaximumContentLength = 10_000;

    private Post()
    {
    }

    private Post(
        Guid id,
        Guid authorUserId,
        string content,
        PostPrivacy privacy,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        AuthorUserId = authorUserId;
        Content = NormalizeContent(content);
        Privacy = privacy;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public PostPrivacy Privacy { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public static Post Create(
        Guid id,
        Guid authorUserId,
        string content,
        PostPrivacy privacy,
        DateTimeOffset createdAtUtc) =>
        new(id, authorUserId, content, privacy, createdAtUtc);

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
