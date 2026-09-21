using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Posts.Data.Configurations;

internal sealed class CommentReactionConfiguration : IEntityTypeConfiguration<CommentReaction>
{
    public void Configure(EntityTypeBuilder<CommentReaction> builder)
    {
        builder.ToTable("CommentReactions");
        builder.HasKey(reaction => new { reaction.CommentId, reaction.UserId });
        builder.Property(reaction => reaction.Type).HasConversion<int>().IsRequired();
        builder.Property(reaction => reaction.CreatedAtUtc).IsRequired();
        builder.HasIndex(reaction => new { reaction.CommentId, reaction.Type });
        builder.HasOne<Comment>()
            .WithMany()
            .HasForeignKey(reaction => reaction.CommentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
