using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Stories.Entities;

[Table("StoryViews")]
[PrimaryKey(nameof(StoryId), nameof(ViewerUserId))]
[Index(nameof(StoryId), nameof(ViewedAtUtc), nameof(ViewerUserId))]
[Index(nameof(ViewerUserId), nameof(ViewedAtUtc))]
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

    [ForeignKey(nameof(StoryId))]
    [InverseProperty(nameof(Story.Views))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Story Story { get; private set; } = null!;

    [ForeignKey(nameof(ViewerUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User ViewerUser { get; private set; } = null!;

    public static StoryView Create(Guid storyId, Guid viewerUserId, DateTimeOffset viewedAtUtc) =>
        new(storyId, viewerUserId, viewedAtUtc);

    public void Refresh(DateTimeOffset viewedAtUtc) => ViewedAtUtc = viewedAtUtc;
}
