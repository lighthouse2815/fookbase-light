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
    }
}
