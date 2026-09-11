using Fookbase.Api.Modules.Stories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Stories.Data.Configurations;

internal sealed class StoryViewConfiguration : IEntityTypeConfiguration<StoryView>
{
    public void Configure(EntityTypeBuilder<StoryView> builder)
    {
        builder.ToTable("StoryViews");
        builder.HasKey(view => new { view.StoryId, view.ViewerUserId });
        builder.Property(view => view.ViewedAtUtc).IsRequired();
        builder.HasIndex(view => new { view.StoryId, view.ViewedAtUtc, view.ViewerUserId });
        builder.HasIndex(view => new { view.ViewerUserId, view.ViewedAtUtc });
        builder.HasOne<Story>().WithMany().HasForeignKey(view => view.StoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
