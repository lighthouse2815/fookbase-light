namespace Fookbase.Api.Modules.Stories.Entities;

public sealed class StoryView
{
    private StoryView() { }

    private StoryView(Guid storyId, Guid viewerUserId, DateTimeOffset viewedAtUtc)
    {
        StoryId = storyId;
        ViewerUserId = viewerUserId;
        ViewedAtUtc = viewedAtUtc;
    }

    public Guid StoryId { get; private set; }
    public Guid ViewerUserId { get; private set; }
    public DateTimeOffset ViewedAtUtc { get; private set; }

    public static StoryView Create(Guid storyId, Guid viewerUserId, DateTimeOffset viewedAtUtc) =>
        new(storyId, viewerUserId, viewedAtUtc);

    public void Refresh(DateTimeOffset viewedAtUtc) => ViewedAtUtc = viewedAtUtc;
}
