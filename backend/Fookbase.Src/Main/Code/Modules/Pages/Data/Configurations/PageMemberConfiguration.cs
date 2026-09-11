using Fookbase.Api.Modules.Pages.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Pages.Data.Configurations;

internal sealed class PageMemberConfiguration : IEntityTypeConfiguration<PageMember>
{
    public void Configure(EntityTypeBuilder<PageMember> builder)
    {
        builder.ToTable("PageMembers");
        builder.HasKey(member => new { member.PageId, member.UserId });
        builder.Property(member => member.Role).HasConversion<int>().IsRequired();
        builder.Property(member => member.JoinedAtUtc).IsRequired();
        builder.HasIndex(member => new { member.UserId, member.PageId });
        builder.HasIndex(member => new { member.PageId, member.Role });
    }
}
