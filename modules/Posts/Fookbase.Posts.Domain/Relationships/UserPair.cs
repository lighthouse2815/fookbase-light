namespace Fookbase.Posts.Domain.Relationships;

public readonly record struct UserPair
{
    private UserPair(Guid userId1, Guid userId2)
    {
        UserId1 = userId1;
        UserId2 = userId2;
    }

    public Guid UserId1 { get; }

    public Guid UserId2 { get; }

    public static UserPair Create(Guid firstUserId, Guid secondUserId)
    {
        if (firstUserId == secondUserId)
        {
            throw new ArgumentException("A relationship requires two different users.");
        }

        return firstUserId.CompareTo(secondUserId) < 0
            ? new UserPair(firstUserId, secondUserId)
            : new UserPair(secondUserId, firstUserId);
    }
}
