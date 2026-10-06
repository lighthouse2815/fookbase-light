using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Reels.Entities;

[Index(nameof(ReelPostId), nameof(ViewedAtUtc))]
[Index(nameof(ReelPostId), nameof(Completed), nameof(ViewedAtUtc))]
[Index(nameof(ViewerUserId), nameof(ViewedAtUtc))]
public sealed class ReelView
{
    private ReelView() { }

    public ReelView(
        Guid id,
        Guid reelPostId,
        Guid viewerUserId,
        int watchDurationMs,
        bool completed,
        bool replayed,
        DateTimeOffset viewedAtUtc)
    {
        Id = id;
        ReelPostId = reelPostId;
        ViewerUserId = viewerUserId;
        WatchDurationMs = watchDurationMs;
        Completed = completed;
        Replayed = replayed;
        ViewedAtUtc = viewedAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }

    public Guid ReelPostId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Post ReelPost { get; private set; } = null!;

    public Guid ViewerUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User ViewerUser { get; private set; } = null!;

    public int WatchDurationMs { get; private set; }

    public bool Completed { get; private set; }

    public bool Replayed { get; private set; }

    public DateTimeOffset ViewedAtUtc { get; private set; }
}
