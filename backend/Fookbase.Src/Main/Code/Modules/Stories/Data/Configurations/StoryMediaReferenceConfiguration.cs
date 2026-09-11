using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Stories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Stories.Data.Configurations;

internal sealed class StoryMediaReferenceConfiguration : IEntityTypeConfiguration<StoryMediaReference>
{
    public void Configure(EntityTypeBuilder<StoryMediaReference> builder)
    {
        builder.ToTable("StoryMediaReferences");
        builder.HasKey(reference => reference.StoryId);
        builder.Property(reference => reference.AttachedAtUtc).IsRequired();
        builder.HasIndex(reference => reference.MediaId);
        builder.HasOne<Story>().WithMany().HasForeignKey(reference => reference.StoryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(reference => reference.MediaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
