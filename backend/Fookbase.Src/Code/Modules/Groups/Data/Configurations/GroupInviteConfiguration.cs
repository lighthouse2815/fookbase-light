using Fookbase.Api.Modules.Groups.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Groups.Data.Configurations;

internal sealed class GroupInviteConfiguration : IEntityTypeConfiguration<GroupInvite>
{
    public void Configure(EntityTypeBuilder<GroupInvite> builder)
    {
        builder.ToTable("GroupInvites");
        builder.HasKey(invite => invite.Id);
        builder.Property(invite => invite.Status).HasConversion<int>().IsRequired();
        builder.Property(invite => invite.CreatedAtUtc).IsRequired();
        builder.HasIndex(invite => new { invite.GroupId, invite.InviteeUserId })
            .IsUnique()
            .HasFilter("\"Status\" = 0");
        builder.HasIndex(invite => new { invite.InviteeUserId, invite.Status, invite.CreatedAtUtc });
    }
}
