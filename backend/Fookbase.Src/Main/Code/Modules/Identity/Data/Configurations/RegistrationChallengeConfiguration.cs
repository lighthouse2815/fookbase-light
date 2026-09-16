using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Identity.Data.Configurations;

internal sealed class RegistrationChallengeConfiguration : IEntityTypeConfiguration<RegistrationChallenge>
{
    public void Configure(EntityTypeBuilder<RegistrationChallenge> builder)
    {
        builder.ToTable("RegistrationChallenges");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Contact).HasMaxLength(256).IsRequired();
        builder.Property(item => item.CodeHash).HasMaxLength(64).IsRequired();
        builder.Property(item => item.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(item => item.FirstName).HasMaxLength(50).IsRequired();
        builder.Property(item => item.LastName).HasMaxLength(50).IsRequired();
        builder.HasIndex(item => item.Contact).IsUnique();
    }
}
