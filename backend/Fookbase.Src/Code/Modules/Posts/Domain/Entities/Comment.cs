using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Entities;

[Table("Comments")]
[Index(nameof(PostId), nameof(DeletedAtUtc), nameof(CreatedAtUtc))]
[Index(nameof(AuthorUserId), nameof(DeletedAtUtc), nameof(CreatedAtUtc), nameof(PostId))]
public sealed class Comment
{
    public const int MaximumContentLength = 5_000;

    private Comment()
    {
    }

    private Comment(
        Guid id,
        Guid postId,
        Guid authorUserId,
        Guid? parentCommentId,
        string content,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        PostId = postId;
        AuthorUserId = authorUserId;
        ParentCommentId = parentCommentId;
        Content = NormalizeContent(content);
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid PostId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public Guid? ParentCommentId { get; private set; }

    [Required]
    [MaxLength(MaximumContentLength)]
    public string Content { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    [ForeignKey(nameof(PostId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Post Post { get; private set; } = null!;

    [ForeignKey(nameof(ParentCommentId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Comment? ParentComment { get; private set; }

    public static Comment Create(
        Guid id,
        Guid postId,
        Guid authorUserId,
        Guid? parentCommentId,
        string content,
        DateTimeOffset createdAtUtc) =>
        new(id, postId, authorUserId, parentCommentId, content, createdAtUtc);

    public void Update(string content, DateTimeOffset updatedAtUtc)
    {
        EnsureActive();
        Content = NormalizeContent(content);
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
            throw new InvalidOperationException("A deleted comment cannot be changed.");
        }
    }

    private static string NormalizeContent(string content)
    {
        var normalized = content.Trim();
        if (normalized.Length is 0 or > MaximumContentLength)
        {
            throw new ArgumentException(
                $"Comment content must contain between 1 and {MaximumContentLength} characters.");
        }

        return normalized;
    }
}
