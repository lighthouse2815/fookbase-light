using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Users.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Users.Entities;

public sealed class UserPrivacySettings
{
    private UserPrivacySettings() { }

    public UserPrivacySettings(Guid userId, DateTimeOffset now)
    {
        UserId = userId;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Guid UserId { get; private set; }

    [InverseProperty(nameof(User.PrivacySettings))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public User User { get; private set; } = null!;

    [Required]
    public PostPrivacy DefaultPostPrivacy { get; private set; } = PostPrivacy.PUBLIC;

    [Required]
    public FriendRequestPolicy FriendRequestPolicy { get; private set; } = FriendRequestPolicy.EVERYONE;

    [Required]
    public RelationshipListVisibility FriendListVisibility { get; private set; } = RelationshipListVisibility.PUBLIC;

    [Required]
    public RelationshipListVisibility FollowListVisibility { get; private set; } = RelationshipListVisibility.PUBLIC;

    [Required]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [Required]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

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
