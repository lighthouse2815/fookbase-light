using Fookbase.Posts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Posts.Infrastructure.Persistence.Configurations;
internal sealed class PostMediaConfiguration : IEntityTypeConfiguration<PostMedia>
{
    public void Configure(EntityTypeBuilder<PostMedia> builder)
    {
        builder.ToTable("PostMedia"); builder.HasKey(x => new { x.PostId, x.MediaId });
        builder.HasIndex(x => new { x.PostId, x.SortOrder }).IsUnique();
        builder.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
    }
}
