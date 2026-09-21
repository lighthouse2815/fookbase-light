using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Posts.Data.Configurations;

internal sealed class PostHashtagConfiguration : IEntityTypeConfiguration<PostHashtag>
{
    public void Configure(EntityTypeBuilder<PostHashtag> builder)
    {
        builder.ToTable("PostHashtags");
        builder.HasKey(postHashtag => new { postHashtag.PostId, postHashtag.HashtagId });
        builder.HasIndex(postHashtag => new { postHashtag.HashtagId, postHashtag.PostId });
        builder.HasOne<Post>()
            .WithMany()
            .HasForeignKey(postHashtag => postHashtag.PostId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Hashtag>()
            .WithMany()
            .HasForeignKey(postHashtag => postHashtag.HashtagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
