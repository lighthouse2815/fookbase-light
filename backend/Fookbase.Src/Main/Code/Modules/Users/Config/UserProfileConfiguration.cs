using Fookbase.Api.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Users.Config;

internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles");
        builder.HasKey(profile => profile.UserId);
        builder.Property(profile => profile.Username).HasMaxLength(32).IsRequired();
        builder.Property(profile => profile.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(profile => profile.Bio).HasMaxLength(500);
        builder.Property(profile => profile.AvatarUrl).HasMaxLength(2048);
        builder.Property(profile => profile.CoverUrl).HasMaxLength(2048);
        builder.Property(profile => profile.CurrentCity).HasMaxLength(100);
        builder.Property(profile => profile.CreatedAt).IsRequired();
        builder.Property(profile => profile.UpdatedAt).IsRequired();
        builder.HasIndex(profile => profile.Username).IsUnique();
    }
}
