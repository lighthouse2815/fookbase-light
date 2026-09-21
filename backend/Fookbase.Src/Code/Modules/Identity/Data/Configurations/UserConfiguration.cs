using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Identity.Data.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(user => user.CreatedAt).IsRequired();
        builder.Property(user => user.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName("EmailIndex")
            .IsUnique();

        builder.HasIndex(user => user.PhoneNumber)
            .HasDatabaseName("PhoneNumberIndex")
            .IsUnique();
    }
}
