using Fookbase.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Media.Infrastructure.Persistence.Configurations;

internal sealed class KnownUserConfiguration : IEntityTypeConfiguration<KnownUser>
{
    public void Configure(EntityTypeBuilder<KnownUser> builder)
    {
        builder.ToTable("KnownUsers"); builder.HasKey(x => x.UserId);
        builder.Property(x => x.Username).HasMaxLength(100).IsRequired();
    }
}
