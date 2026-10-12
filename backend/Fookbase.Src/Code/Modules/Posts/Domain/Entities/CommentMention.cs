using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Entities;

[PrimaryKey(nameof(CommentId), nameof(StartIndex))]
[Index(nameof(CommentId), nameof(MentionedUserId))]
[Index(nameof(MentionedUserId))]
public sealed class CommentMention
{
    private CommentMention() { }

    public CommentMention(Guid commentId, Guid mentionedUserId, int startIndex, int length)
    {
        CommentId = commentId;
        MentionedUserId = mentionedUserId;
        StartIndex = startIndex;
        Length = length;
    }

    public Guid CommentId { get; private set; }

    [InverseProperty(nameof(Comment.Mentions))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Comment Comment { get; private set; } = null!;

    public Guid MentionedUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Cascade)]
    public User MentionedUser { get; private set; } = null!;

    public int StartIndex { get; private set; }

    public int Length { get; private set; }
}
