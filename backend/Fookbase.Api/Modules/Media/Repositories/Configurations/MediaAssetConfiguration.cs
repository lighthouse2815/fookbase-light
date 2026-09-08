using Fookbase.Api.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Media.Repositories.Configurations;

internal sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("MediaAssets");
        builder.HasKey(asset => asset.Id);
        builder.Property(asset => asset.ObjectKey).HasMaxLength(256).IsRequired();
        builder.Property(asset => asset.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(asset => asset.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(asset => asset.MediaType).HasConversion<int>().IsRequired();
        builder.Property(asset => asset.Status).HasConversion<int>().IsRequired();
        builder.Property(asset => asset.CreatedAtUtc).IsRequired();
        builder.HasIndex(asset => asset.ObjectKey).IsUnique();
        builder.HasIndex(asset => new { asset.OwnerUserId, asset.CreatedAtUtc });
        builder.HasIndex(asset => new { asset.Status, asset.UploadExpiresAtUtc });
    }
}
