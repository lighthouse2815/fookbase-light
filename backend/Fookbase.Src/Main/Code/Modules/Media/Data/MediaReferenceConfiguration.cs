using Fookbase.Api.Modules.Media.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Media.Data;

internal sealed class MediaReferenceConfiguration : IEntityTypeConfiguration<MediaReference>
{
    public void Configure(EntityTypeBuilder<MediaReference> builder)
    {
        builder.ToTable("MediaReferences");
        builder.HasKey(x => new { x.MediaId, x.PostId });
        builder.HasIndex(x => x.PostId);
    }
}
