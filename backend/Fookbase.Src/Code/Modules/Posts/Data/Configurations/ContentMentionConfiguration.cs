using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Posts.Data.Configurations;

internal sealed class ContentMentionConfiguration : IEntityTypeConfiguration<ContentMention>
{
    public void Configure(EntityTypeBuilder<ContentMention> builder)
    {
        builder.ToTable("ContentMentions");
        builder.HasKey(mention => new { mention.SourceType, mention.SourceId, mention.StartIndex });
        builder.Property(mention => mention.SourceType).HasConversion<int>().IsRequired();
        builder.HasIndex(mention => new { mention.SourceType, mention.SourceId, mention.MentionedUserId });
        builder.HasIndex(mention => mention.MentionedUserId);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(mention => mention.MentionedUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
