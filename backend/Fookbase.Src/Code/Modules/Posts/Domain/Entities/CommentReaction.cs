using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Entities;

[PrimaryKey(nameof(CommentId), nameof(UserId))]
[Index(nameof(CommentId), nameof(Type))]
public sealed class CommentReaction
{
    private CommentReaction() { }

    public CommentReaction(
        Guid commentId,
        Guid userId,
        ReactionType type,
        DateTimeOffset createdAtUtc)
    {
        CommentId = commentId;
        UserId = userId;
        Type = type;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid CommentId { get; private set; }

    [ForeignKey(nameof(CommentId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Comment Comment { get; private set; } = null!;

    public Guid UserId { get; private set; }

    public ReactionType Type { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public void ChangeTo(ReactionType type, DateTimeOffset updatedAtUtc)
    {
        Type = type;
        UpdatedAtUtc = updatedAtUtc;
    }
}
