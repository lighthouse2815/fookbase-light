using Fookbase.Api.Modules.Media.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Media.Data;

internal sealed class KnownUserConfiguration : IEntityTypeConfiguration<KnownUser>
{
    public void Configure(EntityTypeBuilder<KnownUser> builder)
    {
        builder.ToTable("KnownUsers"); builder.HasKey(x => x.UserId);
        builder.Property(x => x.Username).HasMaxLength(100).IsRequired();
    }
}
