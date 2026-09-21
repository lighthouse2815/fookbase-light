using Fookbase.Api.Modules.Photos.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Photos.Data.Configurations;

internal sealed class PhotoAlbumConfiguration : IEntityTypeConfiguration<PhotoAlbum>
{
    public void Configure(EntityTypeBuilder<PhotoAlbum> builder)
    {
        builder.ToTable("PhotoAlbums");
        builder.HasKey(album => album.Id);
        builder.Property(album => album.Name).HasMaxLength(PhotoAlbum.MaximumNameLength).IsRequired();
        builder.Property(album => album.Description).HasMaxLength(PhotoAlbum.MaximumDescriptionLength);
        builder.Property(album => album.AlbumType).HasConversion<int>();
        builder.Property(album => album.Privacy).HasConversion<int>();
        builder.Property(album => album.CreatedAtUtc).IsRequired();
        builder.Property(album => album.UpdatedAtUtc).IsRequired();
        builder.HasIndex(album => new { album.OwnerUserId, album.AlbumType })
            .IsUnique()
            .HasFilter("\"DeletedAtUtc\" IS NULL AND \"AlbumType\" <> 0");
        builder.HasIndex(album => new { album.OwnerUserId, album.CreatedAtUtc, album.Id })
            .HasFilter("\"DeletedAtUtc\" IS NULL");
    }
}
