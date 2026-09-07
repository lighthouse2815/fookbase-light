using Fookbase.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Media.Infrastructure.Persistence.Configurations;

internal sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("MediaAssets");
        builder.HasKey(asset => asset.Id);
        builder.Property(asset => asset.ObjectName).HasMaxLength(256).IsRequired();
        builder.Property(asset => asset.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(asset => asset.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(asset => asset.Purpose).HasConversion<int>().IsRequired();
        builder.Property(asset => asset.CreatedAt).IsRequired();
        builder.HasIndex(asset => asset.ObjectName).IsUnique();
        builder.HasIndex(asset => new { asset.OwnerUserId, asset.CreatedAt });
    }
}
