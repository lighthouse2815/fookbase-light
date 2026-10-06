using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Stories.Entities;

[PrimaryKey(nameof(StoryId), nameof(ViewerUserId))]
[Index(nameof(StoryId), nameof(ViewedAtUtc), nameof(ViewerUserId))]
[Index(nameof(ViewerUserId), nameof(ViewedAtUtc))]
public sealed class StoryView
{
    private StoryView() { }

    public StoryView(Guid storyId, Guid viewerUserId, DateTimeOffset viewedAtUtc)
    {
        StoryId = storyId;
        ViewerUserId = viewerUserId;
        ViewedAtUtc = viewedAtUtc;
    }

    public Guid StoryId { get; private set; }

    [InverseProperty(nameof(Story.Views))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Story Story { get; private set; } = null!;

    public Guid ViewerUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User ViewerUser { get; private set; } = null!;

    public DateTimeOffset ViewedAtUtc { get; private set; }

    public void Refresh(DateTimeOffset viewedAtUtc) => ViewedAtUtc = viewedAtUtc;
}
