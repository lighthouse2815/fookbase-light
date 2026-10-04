using Fookbase.Api.Modules.Users.Domain.Enums;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Users.Data.Configurations;

internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.HasOne(profile => profile.User)
            .WithOne()
            .HasForeignKey<UserProfile>(profile => profile.UserId);

        builder.Property(profile => profile.BirthdayVisibility)
            .HasDefaultValue(BirthdayVisibility.ONLY_ME);
        builder.Property(profile => profile.Gender)
            .HasDefaultValue(Gender.PREFER_NOT_TO_SAY);
    }
}
