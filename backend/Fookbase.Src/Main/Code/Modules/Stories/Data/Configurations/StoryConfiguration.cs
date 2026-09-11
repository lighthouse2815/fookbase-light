using Fookbase.Api.Modules.Stories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Stories.Data.Configurations;

internal sealed class StoryConfiguration : IEntityTypeConfiguration<Story>
{
    public void Configure(EntityTypeBuilder<Story> builder)
    {
        builder.ToTable("Stories");
        builder.HasKey(story => story.Id);
        builder.Property(story => story.Caption).HasMaxLength(Story.MaximumCaptionLength);
        builder.Property(story => story.Privacy).HasConversion<int>().IsRequired();
        builder.Property(story => story.CreatedAtUtc).IsRequired();
        builder.Property(story => story.ExpiresAtUtc).IsRequired();
        builder.HasIndex(story => new { story.AuthorUserId, story.ExpiresAtUtc, story.CreatedAtUtc, story.Id });
        builder.HasIndex(story => new { story.ExpiresAtUtc, story.CreatedAtUtc });
        builder.HasIndex(story => story.MediaId);
    }
}
