using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Posts.Data.Configurations;

internal sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments");
        builder.HasKey(comment => comment.Id);
        builder.Property(comment => comment.Content).HasMaxLength(Comment.MaximumContentLength).IsRequired();
        builder.Property(comment => comment.CreatedAtUtc).IsRequired();
        builder.HasIndex(comment => new { comment.PostId, comment.DeletedAtUtc, comment.CreatedAtUtc });
        builder.HasOne<Post>()
            .WithMany()
            .HasForeignKey(comment => comment.PostId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Comment>()
            .WithMany()
            .HasForeignKey(comment => comment.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
