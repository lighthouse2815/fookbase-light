using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Entities;

[PrimaryKey(nameof(PostId), nameof(HashtagId))]
[Index(nameof(HashtagId), nameof(PostId))]
public sealed class PostHashtag
{
    private PostHashtag() { }

    public PostHashtag(Guid postId, Guid hashtagId)
    {
        PostId = postId;
        HashtagId = hashtagId;
    }

    public Guid PostId { get; private set; }

    [ForeignKey(nameof(PostId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Post Post { get; private set; } = null!;

    public Guid HashtagId { get; private set; }

    [ForeignKey(nameof(HashtagId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Hashtag Hashtag { get; private set; } = null!;
}
