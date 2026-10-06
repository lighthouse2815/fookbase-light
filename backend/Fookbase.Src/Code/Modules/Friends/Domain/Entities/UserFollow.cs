using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Persistence.Annotations;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Friends.Entities;

[PrimaryKey(nameof(FollowerUserId), nameof(FollowingUserId))]
[Index(nameof(FollowingUserId), nameof(FollowerUserId))]
[Index(nameof(FollowerUserId), nameof(FollowedAtUtc), nameof(FollowingUserId))]
[Index(nameof(FollowingUserId), nameof(FollowedAtUtc), nameof(FollowerUserId))]
[CheckConstraint("CK_UserFollows_DifferentUsers", "\"FollowerUserId\" <> \"FollowingUserId\"")]
public sealed class UserFollow
{
    private UserFollow() { }

    public UserFollow(
        Guid followerUserId,
        Guid followingUserId,
        DateTimeOffset followedAtUtc)
    {
        if (followerUserId == followingUserId)
        {
            throw new ArgumentException("A user cannot follow themselves.");
        }

        FollowerUserId = followerUserId;
        FollowingUserId = followingUserId;
        FollowedAtUtc = followedAtUtc;
    }

    public Guid FollowerUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User FollowerUser { get; private set; } = null!;

    public Guid FollowingUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User FollowingUser { get; private set; } = null!;

    public DateTimeOffset FollowedAtUtc { get; private set; }
}
