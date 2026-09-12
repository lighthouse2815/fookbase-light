namespace Fookbase.Api.Modules.Posts.Entities;

public enum MentionSourceType
{
    Post,
    Comment
}

public sealed class ContentMention
{
    private ContentMention()
    {
    }

    private ContentMention(
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

    public int StartIndex { get; private set; }

    public int Length { get; private set; }

    public static ContentMention Create(
        MentionSourceType sourceType,
        Guid sourceId,
        Guid mentionedUserId,
        int startIndex,
        int length) =>
        new(sourceType, sourceId, mentionedUserId, startIndex, length);
}
