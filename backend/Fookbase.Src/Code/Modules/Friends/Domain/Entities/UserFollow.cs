using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Friends.Entities;

[PrimaryKey(nameof(FollowerUserId), nameof(FollowingUserId))]
[Index(nameof(FollowingUserId), nameof(FollowerUserId))]
[Index(nameof(FollowerUserId), nameof(FollowedAtUtc), nameof(FollowingUserId))]
[Index(nameof(FollowingUserId), nameof(FollowedAtUtc), nameof(FollowerUserId))]
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

    [ForeignKey(nameof(FollowerUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User FollowerUser { get; private set; } = null!;

    [ForeignKey(nameof(FollowingUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User FollowingUser { get; private set; } = null!;

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
