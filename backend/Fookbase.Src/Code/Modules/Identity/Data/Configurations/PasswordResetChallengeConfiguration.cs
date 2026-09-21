using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Identity.Data.Configurations;

internal sealed class PasswordResetChallengeConfiguration : IEntityTypeConfiguration<PasswordResetChallenge>
{
    public void Configure(EntityTypeBuilder<PasswordResetChallenge> builder)
    {
        builder.ToTable("PasswordResetChallenges");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Contact).HasMaxLength(32).IsRequired();
        builder.Property(item => item.CodeHash).HasMaxLength(64).IsRequired();
        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.ExpiresAtUtc).IsRequired();
        builder.HasIndex(item => item.UserId).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
