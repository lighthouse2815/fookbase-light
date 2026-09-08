namespace Fookbase.Api.Modules.Posts.Models;

public sealed class BlockedEdge
{
    private BlockedEdge()
    {
    }

    public Guid BlockerUserId { get; private set; }

    public Guid BlockedUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset LastChangedAtUtc { get; private set; }

    public bool IsActive { get; private set; }
}
