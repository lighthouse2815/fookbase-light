using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Posts.Common;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Entities;

[Index(nameof(PostId), nameof(DeletedAtUtc), nameof(CreatedAtUtc))]
[Index(nameof(AuthorUserId), nameof(DeletedAtUtc), nameof(CreatedAtUtc), nameof(PostId))]
public sealed class Comment
{
    private Comment() { }

    public Comment(
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
        Content = CommentNormalization.NormalizeContent(content);
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid PostId { get; private set; }

    [InverseProperty(nameof(Post.Comments))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Post Post { get; private set; } = null!;

    public Guid AuthorUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User AuthorUser { get; private set; } = null!;

    public Guid? ParentCommentId { get; private set; }

    [InverseProperty(nameof(Replies))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Comment? ParentComment { get; private set; }

    [Required]
    [MaxLength(5_000)]
    public string Content { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public ICollection<Comment> Replies { get; private set; } = new List<Comment>();

    public ICollection<CommentReaction> Reactions { get; private set; } = new List<CommentReaction>();

    public void Update(string content, DateTimeOffset updatedAtUtc)
    {
        EnsureActive();
        Content = CommentNormalization.NormalizeContent(content);
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
}
