using Fookbase.Api.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Users.Data.Configurations;

internal sealed class UserPrivacySettingsConfiguration : IEntityTypeConfiguration<UserPrivacySettings>
{
    public void Configure(EntityTypeBuilder<UserPrivacySettings> builder)
    {
        builder.ToTable("UserPrivacySettings");
        builder.HasKey(settings => settings.UserId);
        builder.Property(settings => settings.DefaultPostPrivacy).HasConversion<int>().IsRequired();
        builder.Property(settings => settings.FriendRequestPolicy).HasConversion<int>().IsRequired();
        builder.Property(settings => settings.FriendListVisibility).HasConversion<int>().IsRequired();
        builder.Property(settings => settings.FollowListVisibility).HasConversion<int>().IsRequired();
        builder.Property(settings => settings.CreatedAtUtc).IsRequired();
        builder.Property(settings => settings.UpdatedAtUtc).IsRequired();
    }
}
