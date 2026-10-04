using Fookbase.Api.Modules.Users.Domain.Enums;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Users.Data.Configurations;

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
        builder.Property(profile => profile.AvatarMediaId);
        builder.Property(profile => profile.CoverMediaId);
        builder.Property(profile => profile.BirthdayVisibility)
            .HasDefaultValue(BirthdayVisibility.ONLY_ME);
        builder.Property(profile => profile.Gender)
            .HasDefaultValue(Gender.PREFER_NOT_TO_SAY)
            .ValueGeneratedNever();
        builder.Property(profile => profile.CurrentCity).HasMaxLength(100);
        builder.Property(profile => profile.Hometown).HasMaxLength(100);
        builder.Property(profile => profile.Workplace).HasMaxLength(150);
        builder.Property(profile => profile.Education).HasMaxLength(150);
        builder.Property(profile => profile.Website).HasMaxLength(2048);
        builder.Property(profile => profile.CreatedAt).IsRequired();
        builder.Property(profile => profile.UpdatedAt).IsRequired();
        builder.HasIndex(profile => profile.Username).IsUnique();
    }
}
