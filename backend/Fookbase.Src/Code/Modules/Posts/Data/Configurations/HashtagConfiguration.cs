using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Posts.Data.Configurations;

internal sealed class HashtagConfiguration : IEntityTypeConfiguration<Hashtag>
{
    public void Configure(EntityTypeBuilder<Hashtag> builder)
    {
        builder.ToTable("Hashtags");
        builder.HasKey(hashtag => hashtag.Id);
        builder.Property(hashtag => hashtag.NormalizedName).HasMaxLength(Hashtag.MaximumLength).IsRequired();
        builder.Property(hashtag => hashtag.DisplayName).HasMaxLength(Hashtag.MaximumLength).IsRequired();
        builder.Property(hashtag => hashtag.CreatedAtUtc).IsRequired();
        builder.HasIndex(hashtag => hashtag.NormalizedName).IsUnique();
    }
}
