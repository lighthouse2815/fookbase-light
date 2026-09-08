using Fookbase.Api.Modules.Friends.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Friends.Config;

internal sealed class BlockedUserConfiguration : IEntityTypeConfiguration<BlockedUser>
{
    public void Configure(EntityTypeBuilder<BlockedUser> builder)
    {
        builder.ToTable("BlockedUsers", table => table.HasCheckConstraint(
            "CK_BlockedUsers_DifferentUsers",
            "\"BlockerUserId\" <> \"BlockedUserId\""));
        builder.HasKey(block => new { block.BlockerUserId, block.BlockedUserId });
        builder.Property(block => block.CreatedAtUtc).IsRequired();
        builder.HasIndex(block => block.BlockedUserId);
    }
}
