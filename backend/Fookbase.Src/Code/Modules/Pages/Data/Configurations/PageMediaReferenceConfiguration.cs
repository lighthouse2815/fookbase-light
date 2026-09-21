using Fookbase.Api.Modules.Pages.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Pages.Data.Configurations;

internal sealed class PageMediaReferenceConfiguration : IEntityTypeConfiguration<PageMediaReference>
{
    public void Configure(EntityTypeBuilder<PageMediaReference> builder)
    {
        builder.ToTable("PageMediaReferences");
        builder.HasKey(reference => new { reference.PageId, reference.Slot });
        builder.Property(reference => reference.Slot).HasConversion<int>().IsRequired();
        builder.Property(reference => reference.AttachedAtUtc).IsRequired();
        builder.HasIndex(reference => reference.MediaId);
    }
}
