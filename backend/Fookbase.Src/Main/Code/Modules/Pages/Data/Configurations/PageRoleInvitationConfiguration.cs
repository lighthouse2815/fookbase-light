using Fookbase.Api.Modules.Pages.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Pages.Data.Configurations;

internal sealed class PageRoleInvitationConfiguration : IEntityTypeConfiguration<PageRoleInvitation>
{
    public void Configure(EntityTypeBuilder<PageRoleInvitation> builder)
    {
        builder.ToTable("PageRoleInvitations");
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Role).HasConversion<int>().IsRequired();
        builder.Property(invitation => invitation.Status).HasConversion<int>().IsRequired();
        builder.Property(invitation => invitation.CreatedAtUtc).IsRequired();
        builder.HasIndex(invitation => new { invitation.PageId, invitation.InviteeUserId })
            .IsUnique().HasFilter("\"Status\" = 0");
        builder.HasIndex(invitation => new { invitation.InviteeUserId, invitation.Status, invitation.CreatedAtUtc });
    }
}
