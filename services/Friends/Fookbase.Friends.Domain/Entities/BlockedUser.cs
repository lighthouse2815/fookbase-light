namespace Fookbase.Friends.Domain.Entities;

public sealed class BlockedUser
{
    private BlockedUser()
    {
    }

    private BlockedUser(Guid blockerUserId, Guid blockedUserId, DateTimeOffset createdAtUtc)
    {
        BlockerUserId = blockerUserId;
        BlockedUserId = blockedUserId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid BlockerUserId { get; private set; }

    public Guid BlockedUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static BlockedUser Create(
        Guid blockerUserId,
        Guid blockedUserId,
        DateTimeOffset createdAtUtc)
    {
        if (blockerUserId == blockedUserId)
        {
            throw new ArgumentException("A user cannot block themselves.");
        }

        return new BlockedUser(blockerUserId, blockedUserId, createdAtUtc);
    }
}
