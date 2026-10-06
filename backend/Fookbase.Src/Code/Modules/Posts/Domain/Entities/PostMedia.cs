using System.ComponentModel.DataAnnotations.Schema;
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

    [ForeignKey(nameof(PostId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Post Post { get; private set; } = null!;

    public Guid MediaId { get; private set; }

    public int SortOrder { get; private set; }

    public void ChangeSortOrder(int sortOrder) => SortOrder = sortOrder;
}
