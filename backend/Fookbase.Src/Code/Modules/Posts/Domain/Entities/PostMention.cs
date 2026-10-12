using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Entities;

[PrimaryKey(nameof(PostId), nameof(StartIndex))]
[Index(nameof(PostId), nameof(MentionedUserId))]
[Index(nameof(MentionedUserId))]
public sealed class PostMention
{
    private PostMention() { }

    public PostMention(Guid postId, Guid mentionedUserId, int startIndex, int length)
    {
        PostId = postId;
        MentionedUserId = mentionedUserId;
        StartIndex = startIndex;
        Length = length;
    }

    public Guid PostId { get; private set; }

    [InverseProperty(nameof(Post.Mentions))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Post Post { get; private set; } = null!;

    public Guid MentionedUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Cascade)]
    public User MentionedUser { get; private set; } = null!;

    public int StartIndex { get; private set; }

    public int Length { get; private set; }
}
