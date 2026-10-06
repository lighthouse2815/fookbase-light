using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Entities;

[PrimaryKey(nameof(SourceType), nameof(SourceId), nameof(StartIndex))]
[Index(nameof(SourceType), nameof(SourceId), nameof(MentionedUserId))]
[Index(nameof(MentionedUserId))]
public sealed class ContentMention
{
    private ContentMention() { }

    public ContentMention(
        MentionSourceType sourceType,
        Guid sourceId,
        Guid mentionedUserId,
        int startIndex,
        int length)
    {
        SourceType = sourceType;
        SourceId = sourceId;
        MentionedUserId = mentionedUserId;
        StartIndex = startIndex;
        Length = length;
    }

    public MentionSourceType SourceType { get; private set; }

    public Guid SourceId { get; private set; }

    public Guid MentionedUserId { get; private set; }

    [ForeignKey(nameof(MentionedUserId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public User MentionedUser { get; private set; } = null!;

    public int StartIndex { get; private set; }

    public int Length { get; private set; }
}
