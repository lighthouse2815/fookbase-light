namespace Fookbase.Api.Modules.Friends.Entities;

public sealed class UserFollow
{
    private UserFollow()
    {
    }

    private UserFollow(Guid followerUserId, Guid followingUserId, DateTimeOffset followedAtUtc)
    {
        FollowerUserId = followerUserId;
        FollowingUserId = followingUserId;
        FollowedAtUtc = followedAtUtc;
    }

    public Guid FollowerUserId { get; private set; }

    public Guid FollowingUserId { get; private set; }

    public DateTimeOffset FollowedAtUtc { get; private set; }

    public static UserFollow Create(
        Guid followerUserId,
        Guid followingUserId,
        DateTimeOffset followedAtUtc)
    {
        if (followerUserId == followingUserId)
        {
            throw new ArgumentException("A user cannot follow themselves.");
        }

        return new UserFollow(followerUserId, followingUserId, followedAtUtc);
    }
}
