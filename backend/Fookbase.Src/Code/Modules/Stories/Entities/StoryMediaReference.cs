namespace Fookbase.Api.Modules.Stories.Entities;

public sealed class StoryMediaReference
{
    private StoryMediaReference() { }

    private StoryMediaReference(Guid storyId, Guid mediaId, DateTimeOffset attachedAtUtc)
    {
        StoryId = storyId;
        MediaId = mediaId;
        AttachedAtUtc = attachedAtUtc;
    }

    public Guid StoryId { get; private set; }
    public Guid MediaId { get; private set; }
    public DateTimeOffset AttachedAtUtc { get; private set; }

    public static StoryMediaReference Create(Guid storyId, Guid mediaId, DateTimeOffset attachedAtUtc) =>
        new(storyId, mediaId, attachedAtUtc);
}
