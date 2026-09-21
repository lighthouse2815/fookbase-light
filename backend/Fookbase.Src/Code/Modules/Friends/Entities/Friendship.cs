using Fookbase.Api.Modules.Friends.Entities;

namespace Fookbase.Api.Modules.Friends.Entities;

public sealed class Friendship
{
    private Friendship()
    {
    }

    private Friendship(Guid id, UserPair pair, DateTimeOffset createdAtUtc)
    {
        Id = id;
        UserId1 = pair.UserId1;
        UserId2 = pair.UserId2;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid UserId1 { get; private set; }

    public Guid UserId2 { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Friendship Create(
        Guid id,
        Guid firstUserId,
        Guid secondUserId,
        DateTimeOffset createdAtUtc) =>
        new(id, UserPair.Create(firstUserId, secondUserId), createdAtUtc);

    public bool Contains(Guid userId) => UserId1 == userId || UserId2 == userId;

    public Guid OtherUserId(Guid userId) =>
        UserId1 == userId
            ? UserId2
            : UserId2 == userId
                ? UserId1
                : throw new UnauthorizedAccessException("The user is not part of this friendship.");
}
