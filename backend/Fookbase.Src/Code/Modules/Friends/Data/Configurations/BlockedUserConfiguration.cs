using Fookbase.Api.Modules.Friends.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Friends.Data.Configurations;

internal sealed class BlockedUserConfiguration : IEntityTypeConfiguration<BlockedUser>
{
    public void Configure(EntityTypeBuilder<BlockedUser> builder)
    {
        builder.ToTable("BlockedUsers", table => table.HasCheckConstraint(
            "CK_BlockedUsers_DifferentUsers",
            "\"BlockerUserId\" <> \"BlockedUserId\""));
    }
}
