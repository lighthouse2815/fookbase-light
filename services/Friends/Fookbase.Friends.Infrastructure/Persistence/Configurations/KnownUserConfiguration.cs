using Fookbase.Friends.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Friends.Infrastructure.Persistence.Configurations;

internal sealed class KnownUserConfiguration : IEntityTypeConfiguration<KnownUser>
{
    public void Configure(EntityTypeBuilder<KnownUser> builder)
    {
        builder.ToTable("KnownUsers");
        builder.HasKey(user => user.UserId);
        builder.Property(user => user.Username).HasMaxLength(32).IsRequired();
        builder.Property(user => user.CreatedAtUtc).IsRequired();
    }
}
