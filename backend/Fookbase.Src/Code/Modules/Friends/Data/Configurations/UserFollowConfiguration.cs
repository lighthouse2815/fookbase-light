using Fookbase.Api.Modules.Friends.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Friends.Data.Configurations;

internal sealed class UserFollowConfiguration : IEntityTypeConfiguration<UserFollow>
{
    public void Configure(EntityTypeBuilder<UserFollow> builder)
    {
        builder.ToTable("UserFollows", table => table.HasCheckConstraint(
            "CK_UserFollows_DifferentUsers",
            "\"FollowerUserId\" <> \"FollowingUserId\""));
        builder.HasKey(follow => new { follow.FollowerUserId, follow.FollowingUserId });
        builder.Property(follow => follow.FollowedAtUtc).IsRequired();
        builder.HasIndex(follow => new { follow.FollowingUserId, follow.FollowerUserId });
        builder.HasIndex(follow => new
        {
            follow.FollowerUserId,
            follow.FollowedAtUtc,
            follow.FollowingUserId
        });
        builder.HasIndex(follow => new
        {
            follow.FollowingUserId,
            follow.FollowedAtUtc,
            follow.FollowerUserId
        });
    }
}
