using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Posts.Data.Configurations;

internal sealed class PostShareConfiguration : IEntityTypeConfiguration<PostShare>
{
    public void Configure(EntityTypeBuilder<PostShare> builder)
    {
        builder.ToTable("PostShares");
        builder.HasKey(share => share.Id);
        builder.Property(share => share.DestinationType).HasConversion<int>().IsRequired();
        builder.Property(share => share.Caption).HasMaxLength(PostShare.MaximumCaptionLength);
        builder.Property(share => share.CreatedAtUtc).IsRequired();
        builder.HasIndex(share => new { share.OriginalPostId, share.DeletedAtUtc });
        builder.HasIndex(share => new { share.SharingUserId, share.DeletedAtUtc, share.CreatedAtUtc, share.OriginalPostId });
        builder.HasIndex(share => new
        {
            share.DestinationType,
            share.DestinationId,
            share.DeletedAtUtc,
            share.CreatedAtUtc,
            share.Id
        });
        builder.HasOne<Post>()
            .WithMany()
            .HasForeignKey(share => share.OriginalPostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
