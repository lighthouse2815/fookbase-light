using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Identity.Data.Configurations;

internal sealed class ExternalLoginCompletionConfiguration : IEntityTypeConfiguration<ExternalLoginCompletion>
{
    public void Configure(EntityTypeBuilder<ExternalLoginCompletion> builder)
    {
        builder.ToTable("ExternalLoginCompletions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.CodeHash).HasMaxLength(64).IsRequired();
        builder.Property(item => item.Client).HasMaxLength(32).IsRequired();
        builder.Property(item => item.Provider).HasMaxLength(32).IsRequired();
        builder.Property(item => item.ProviderKey).HasMaxLength(256).IsRequired();
        builder.Property(item => item.Email).HasMaxLength(256).IsRequired();
        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.ExpiresAtUtc).IsRequired();
        builder.HasIndex(item => new { item.CodeHash, item.ExpiresAtUtc }).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
