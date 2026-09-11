namespace Fookbase.Api.Modules.Reels.Entities;

public sealed class ReelView
{
    private ReelView() { }

    private ReelView(
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

    public Guid Id { get; private set; }
    public Guid ReelPostId { get; private set; }
    public Guid ViewerUserId { get; private set; }
    public int WatchDurationMs { get; private set; }
    public bool Completed { get; private set; }
    public bool Replayed { get; private set; }
    public DateTimeOffset ViewedAtUtc { get; private set; }

    public static ReelView Create(
        Guid reelPostId,
        Guid viewerUserId,
        int watchDurationMs,
        bool completed,
        bool replayed,
        DateTimeOffset viewedAtUtc) =>
        new(Guid.NewGuid(), reelPostId, viewerUserId, watchDurationMs, completed, replayed, viewedAtUtc);
}
