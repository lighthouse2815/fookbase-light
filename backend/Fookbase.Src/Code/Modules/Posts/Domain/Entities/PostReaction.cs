using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Entities;

[Table("PostReactions")]
[PrimaryKey(nameof(PostId), nameof(UserId))]
[Index(nameof(PostId), nameof(Type))]
[Index(nameof(UserId), nameof(CreatedAtUtc), nameof(PostId))]
public sealed class PostReaction
{
    private PostReaction()
    {
    }

    public PostReaction(
        Guid postId,
        Guid userId,
        ReactionType type,
        DateTimeOffset createdAtUtc)
    {
        PostId = postId;
        UserId = userId;
        Type = type;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid PostId { get; private set; }

    public Guid UserId { get; private set; }

    public ReactionType Type { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    [ForeignKey(nameof(PostId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Post Post { get; private set; } = null!;

    public void ChangeTo(ReactionType type, DateTimeOffset updatedAtUtc)
    {
        Type = type;
        UpdatedAtUtc = updatedAtUtc;
    }
}
