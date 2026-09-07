using Fookbase.Posts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Posts.Infrastructure.Persistence.Configurations;

internal sealed class PostReactionConfiguration : IEntityTypeConfiguration<PostReaction>
{
    public void Configure(EntityTypeBuilder<PostReaction> builder)
    {
        builder.ToTable("PostReactions");
        builder.HasKey(reaction => new { reaction.PostId, reaction.UserId });
        builder.Property(reaction => reaction.Type).HasConversion<int>().IsRequired();
        builder.Property(reaction => reaction.CreatedAtUtc).IsRequired();
        builder.HasIndex(reaction => new { reaction.PostId, reaction.Type });
        builder.HasOne<Post>()
            .WithMany()
            .HasForeignKey(reaction => reaction.PostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
