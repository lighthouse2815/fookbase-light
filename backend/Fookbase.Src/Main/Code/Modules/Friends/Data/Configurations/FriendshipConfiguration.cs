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
        builder.HasKey(friendship => friendship.Id);
        builder.Property(friendship => friendship.CreatedAtUtc).IsRequired();
        builder.HasIndex(friendship => new { friendship.UserId1, friendship.UserId2 }).IsUnique();
        builder.HasIndex(friendship => friendship.UserId1);
        builder.HasIndex(friendship => friendship.UserId2);
    }
}
