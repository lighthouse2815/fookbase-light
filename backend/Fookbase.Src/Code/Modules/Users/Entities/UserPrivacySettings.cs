using Fookbase.Api.Modules.Posts.Entities;

namespace Fookbase.Api.Modules.Users.Entities;

public sealed class UserPrivacySettings
{
    private UserPrivacySettings()
    {
    }

    private UserPrivacySettings(Guid userId, DateTimeOffset now)
    {
        UserId = userId;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public Guid UserId { get; private set; }

    public PostPrivacy DefaultPostPrivacy { get; private set; } = PostPrivacy.Public;

    public FriendRequestPolicy FriendRequestPolicy { get; private set; } = FriendRequestPolicy.Everyone;

    public RelationshipListVisibility FriendListVisibility { get; private set; } = RelationshipListVisibility.Public;

    public RelationshipListVisibility FollowListVisibility { get; private set; } = RelationshipListVisibility.Public;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static UserPrivacySettings Create(Guid userId, DateTimeOffset now) => new(userId, now);

    public void Update(
        PostPrivacy? defaultPostPrivacy,
        FriendRequestPolicy? friendRequestPolicy,
        RelationshipListVisibility? friendListVisibility,
        RelationshipListVisibility? followListVisibility,
        DateTimeOffset now)
    {
        if (defaultPostPrivacy is { } postPrivacy)
        {
            DefaultPostPrivacy = postPrivacy;
        }

        if (friendRequestPolicy is { } requestPolicy)
        {
            FriendRequestPolicy = requestPolicy;
        }

        if (friendListVisibility is { } friendsVisibility)
        {
            FriendListVisibility = friendsVisibility;
        }

        if (followListVisibility is { } followsVisibility)
        {
            FollowListVisibility = followsVisibility;
        }

        UpdatedAtUtc = now;
    }
}
