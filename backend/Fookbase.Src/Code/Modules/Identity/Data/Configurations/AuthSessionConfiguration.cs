using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Identity.Data.Configurations;

internal sealed class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSession>
{
    public void Configure(EntityTypeBuilder<AuthSession> builder)
    {
        builder.ToTable("AuthSessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.UserAgent).HasMaxLength(256);
        builder.HasIndex(session => new { session.UserId, session.ExpiresAtUtc });
    }
}
