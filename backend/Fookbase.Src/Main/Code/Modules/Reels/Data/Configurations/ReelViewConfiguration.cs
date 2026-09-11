using Fookbase.Api.Modules.Reels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Reels.Data.Configurations;

internal sealed class ReelViewConfiguration : IEntityTypeConfiguration<ReelView>
{
    public void Configure(EntityTypeBuilder<ReelView> builder)
    {
        builder.ToTable("ReelViews");
        builder.HasKey(view => view.Id);
        builder.Property(view => view.ViewedAtUtc).IsRequired();
        builder.HasIndex(view => new { view.ReelPostId, view.ViewedAtUtc });
        builder.HasIndex(view => new { view.ReelPostId, view.Completed, view.ViewedAtUtc });
        builder.HasIndex(view => new { view.ViewerUserId, view.ViewedAtUtc });
    }
}
