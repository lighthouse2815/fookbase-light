using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Media.Entities;

[PrimaryKey(nameof(MediaId), nameof(PostId))]
[Index(nameof(PostId))]
public sealed class MediaReference
{

    private MediaReference()
    {
    }

    public MediaReference(Guid mediaId, Guid postId, DateTimeOffset attachedAtUtc)
    {
        MediaId = mediaId;
        PostId = postId;
        AttachedAtUtc = attachedAtUtc;
    }

    public Guid MediaId { get; private set; }
    public Guid PostId { get; private set; }
    public DateTimeOffset AttachedAtUtc { get; private set; }

    [ForeignKey(nameof(MediaId))]
    [InverseProperty(nameof(MediaAsset.PostReferences))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset Media { get; private set; } = null!;

    [ForeignKey(nameof(PostId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Post Post { get; private set; } = null!;

}
