using Fookbase.Api.Modules.Friends.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Friends.Repositories.Configurations;

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
