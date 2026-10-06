using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Entities;

[PrimaryKey(nameof(PostId), nameof(UserId))]
[Index(nameof(PostId), nameof(Type))]
[Index(nameof(UserId), nameof(CreatedAtUtc), nameof(PostId))]
public sealed class PostReaction
{
    private PostReaction() { }

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

    [ForeignKey(nameof(PostId))]
    [InverseProperty(nameof(Post.Reactions))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Post Post { get; private set; } = null!;

    public Guid UserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User User { get; private set; } = null!;

    public ReactionType Type { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public void ChangeTo(ReactionType type, DateTimeOffset updatedAtUtc)
    {
        Type = type;
        UpdatedAtUtc = updatedAtUtc;
    }
}
