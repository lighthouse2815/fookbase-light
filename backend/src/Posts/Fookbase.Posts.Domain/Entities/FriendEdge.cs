namespace Fookbase.Posts.Domain.Entities;

public sealed class FriendEdge
{
    private FriendEdge()
    {
    }

    public Guid UserId1 { get; private set; }

    public Guid UserId2 { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset LastChangedAtUtc { get; private set; }

    public bool IsActive { get; private set; }
}
