using Fookbase.Api.Modules.Friends.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Friends.Data.Configurations;

internal sealed class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        builder.ToTable("Friendships", table => table.HasCheckConstraint(
            "CK_Friendships_CanonicalPair",
            "\"UserId1\" < \"UserId2\""));
    }
}
