using Fookbase.Api.Modules.Pages.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Pages.Data.Configurations;

internal sealed class PageConfiguration : IEntityTypeConfiguration<Page>
{
    public void Configure(EntityTypeBuilder<Page> builder)
    {
        builder.ToTable("Pages");
        builder.HasKey(page => page.Id);
        builder.Property(page => page.Name).HasMaxLength(Page.MaximumNameLength).IsRequired();
        builder.Property(page => page.Username).HasColumnType("citext").HasMaxLength(Page.MaximumUsernameLength).IsRequired();
        builder.Property(page => page.Category).HasMaxLength(Page.MaximumCategoryLength).IsRequired();
        builder.Property(page => page.Bio).HasMaxLength(Page.MaximumBioLength);
        builder.Property(page => page.Status).HasConversion<int>().IsRequired();
        builder.Property(page => page.CreatedAtUtc).IsRequired();
        builder.HasIndex(page => page.Username).IsUnique().HasFilter("\"DeletedAtUtc\" IS NULL");
        builder.HasIndex(page => new { page.Status, page.Name, page.Id }).HasFilter("\"DeletedAtUtc\" IS NULL");
        builder.HasIndex(page => new { page.CreatedByUserId, page.CreatedAtUtc }).HasFilter("\"DeletedAtUtc\" IS NULL");
    }
}
