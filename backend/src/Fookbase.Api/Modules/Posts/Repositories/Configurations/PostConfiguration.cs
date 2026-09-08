using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Posts.Repositories.Configurations;

internal sealed class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("Posts");
        builder.HasKey(post => post.Id);
        builder.Property(post => post.Content).HasMaxLength(Post.MaximumContentLength).IsRequired();
        builder.Property(post => post.Privacy).HasConversion<int>().IsRequired();
        builder.Property(post => post.CreatedAtUtc).IsRequired();
        builder.HasIndex(post => new { post.AuthorUserId, post.CreatedAtUtc });
        builder.HasIndex(post => new { post.DeletedAtUtc, post.CreatedAtUtc });
    }
}
