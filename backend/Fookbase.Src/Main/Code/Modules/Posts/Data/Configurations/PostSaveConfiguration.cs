using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Posts.Data.Configurations;

internal sealed class PostSaveConfiguration : IEntityTypeConfiguration<PostSave>
{
    public void Configure(EntityTypeBuilder<PostSave> builder)
    {
        builder.ToTable("PostSaves");
        builder.HasKey(save => new { save.UserId, save.PostId });
        builder.Property(save => save.SavedAtUtc).IsRequired();
        builder.HasIndex(save => new { save.UserId, save.SavedAtUtc, save.PostId });
        builder.HasOne<Post>()
            .WithMany()
            .HasForeignKey(save => save.PostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
