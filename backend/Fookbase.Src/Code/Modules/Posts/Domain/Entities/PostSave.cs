using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Entities;

[Table("PostSaves")]
[PrimaryKey(nameof(UserId), nameof(PostId))]
[Index(nameof(UserId), nameof(SavedAtUtc), nameof(PostId))]
public sealed class PostSave
{
    private PostSave()
    {
    }

    private PostSave(Guid userId, Guid postId, DateTimeOffset savedAtUtc)
    {
        UserId = userId;
        PostId = postId;
        SavedAtUtc = savedAtUtc;
    }

    public Guid UserId { get; private set; }

    public Guid PostId { get; private set; }

    public DateTimeOffset SavedAtUtc { get; private set; }

    [ForeignKey(nameof(PostId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Post Post { get; private set; } = null!;

    public static PostSave Create(Guid userId, Guid postId, DateTimeOffset savedAtUtc) =>
        new(userId, postId, savedAtUtc);
}
