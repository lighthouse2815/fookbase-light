using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Entities;

[PrimaryKey(nameof(UserId), nameof(PostId))]
[Index(nameof(UserId), nameof(SavedAtUtc), nameof(PostId))]
public sealed class PostSave
{
    private PostSave() { }

    public PostSave(
        Guid userId,
        Guid postId,
        DateTimeOffset savedAtUtc)
    {
        UserId = userId;
        PostId = postId;
        SavedAtUtc = savedAtUtc;
    }

    public Guid UserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User User { get; private set; } = null!;

    public Guid PostId { get; private set; }

    [InverseProperty(nameof(Post.Saves))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Post Post { get; private set; } = null!;

    public DateTimeOffset SavedAtUtc { get; private set; }
}
