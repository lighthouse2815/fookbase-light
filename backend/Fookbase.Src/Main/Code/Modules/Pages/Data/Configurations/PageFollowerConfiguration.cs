using Fookbase.Api.Modules.Pages.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Pages.Data.Configurations;

internal sealed class PageFollowerConfiguration : IEntityTypeConfiguration<PageFollower>
{
    public void Configure(EntityTypeBuilder<PageFollower> builder)
    {
        builder.ToTable("PageFollowers");
        builder.HasKey(follower => new { follower.PageId, follower.UserId });
        builder.Property(follower => follower.FollowedAtUtc).IsRequired();
        builder.HasIndex(follower => new { follower.UserId, follower.FollowedAtUtc });
    }
}
