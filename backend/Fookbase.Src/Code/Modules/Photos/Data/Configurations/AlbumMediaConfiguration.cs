using Fookbase.Api.Modules.Photos.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Photos.Data.Configurations;

internal sealed class AlbumMediaConfiguration : IEntityTypeConfiguration<AlbumMedia>
{
    public void Configure(EntityTypeBuilder<AlbumMedia> builder)
    {
        builder.ToTable("AlbumMedia");
        builder.HasKey(media => new { media.AlbumId, media.MediaId });
        builder.Property(media => media.Caption).HasMaxLength(AlbumMedia.MaximumCaptionLength);
        builder.Property(media => media.AddedAtUtc).IsRequired();
        builder.HasIndex(media => new { media.AlbumId, media.SortOrder, media.MediaId });
        builder.HasIndex(media => media.MediaId);
    }
}
