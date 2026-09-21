using Fookbase.Api.Modules.Stories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Stories.Data.Configurations;

internal sealed class StoryReactionConfiguration : IEntityTypeConfiguration<StoryReaction>
{
    public void Configure(EntityTypeBuilder<StoryReaction> builder)
    {
        builder.ToTable("StoryReactions");
        builder.HasKey(reaction => new { reaction.StoryId, reaction.UserId });
        builder.Property(reaction => reaction.Type).HasConversion<int>().IsRequired();
        builder.Property(reaction => reaction.CreatedAtUtc).IsRequired();
        builder.HasIndex(reaction => new { reaction.StoryId, reaction.CreatedAtUtc });
        builder.HasOne<Story>().WithMany().HasForeignKey(reaction => reaction.StoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
