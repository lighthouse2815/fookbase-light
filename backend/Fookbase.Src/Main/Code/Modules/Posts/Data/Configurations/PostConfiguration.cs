using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Posts.Data.Configurations;

internal sealed class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("Posts");
        builder.HasKey(post => post.Id);
        builder.Property(post => post.Content).HasMaxLength(Post.MaximumContentLength).IsRequired();
        builder.Property(post => post.Privacy).HasConversion<int>().IsRequired();
        builder.Property(post => post.TextBackground).HasMaxLength(Post.MaximumTextBackgroundLength);
        builder.Property(post => post.ContainerType).HasConversion<int>().IsRequired();
        builder.Property(post => post.ContainerId).IsRequired();
        builder.Property(post => post.PostType).HasConversion<int>().IsRequired();
        builder.Property(post => post.IsPinned).HasDefaultValue(false).IsRequired();
        builder.Property(post => post.CreatedAtUtc).IsRequired();
        builder.HasIndex(post => new { post.AuthorUserId, post.CreatedAtUtc });
        builder.HasIndex(post => new { post.DeletedAtUtc, post.CreatedAtUtc });
        builder.HasIndex(post => new { post.AuthorUserId, post.CreatedAtUtc, post.Id })
            .HasFilter("\"DeletedAtUtc\" IS NULL");
        builder.HasIndex(post => new { post.AuthorUserId, post.IsPinned, post.CreatedAtUtc, post.Id })
            .HasFilter("\"DeletedAtUtc\" IS NULL");
        builder.HasIndex(post => new
        {
            post.ContainerType,
            post.ContainerId,
            post.CreatedAtUtc,
            post.Id
        }).HasFilter("\"DeletedAtUtc\" IS NULL");
        builder.HasIndex(post => new { post.PostType, post.CreatedAtUtc, post.Id })
            .HasFilter("\"DeletedAtUtc\" IS NULL");
        builder.HasIndex(post => new { post.PostType, post.AuthorUserId, post.CreatedAtUtc, post.Id })
            .HasFilter("\"DeletedAtUtc\" IS NULL");
    }
}
