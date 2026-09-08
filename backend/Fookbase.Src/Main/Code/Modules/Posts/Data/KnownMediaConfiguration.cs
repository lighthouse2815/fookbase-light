using Fookbase.Api.Modules.Posts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Posts.Data;
internal sealed class KnownMediaConfiguration : IEntityTypeConfiguration<KnownMedia>
{
    public void Configure(EntityTypeBuilder<KnownMedia> builder)
    {
        builder.ToTable("KnownMedia"); builder.HasKey(x => x.MediaId);
        builder.Property(x => x.MediaType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.OwnerUserId, x.IsReady });
    }
}
