using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Entities;

[PrimaryKey(nameof(PostId), nameof(MediaId))]
[Index(nameof(PostId), nameof(SortOrder), IsUnique = true)]
public sealed class PostMedia
{
    private PostMedia() { }

    public PostMedia(
        Guid postId,
        Guid mediaId,
        int sortOrder)
    {
        PostId = postId;
        MediaId = mediaId;
        SortOrder = sortOrder;
    }

    public Guid PostId { get; private set; }

    [InverseProperty(nameof(Post.MediaItems))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Post Post { get; private set; } = null!;

    public Guid MediaId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset Media { get; private set; } = null!;

    public int SortOrder { get; private set; }

    public void ChangeSortOrder(int sortOrder) => SortOrder = sortOrder;
}
