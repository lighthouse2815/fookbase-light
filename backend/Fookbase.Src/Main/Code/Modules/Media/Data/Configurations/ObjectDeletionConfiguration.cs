using Fookbase.Api.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Media.Data.Configurations;

internal sealed class ObjectDeletionConfiguration : IEntityTypeConfiguration<ObjectDeletion>
{
    public void Configure(EntityTypeBuilder<ObjectDeletion> builder)
    {
        builder.ToTable("ObjectDeletions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ObjectKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.NextAttemptAtUtc).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.HasIndex(x => new { x.ProcessedAtUtc, x.FailedAtUtc, x.NextAttemptAtUtc, x.CreatedAtUtc });
    }
}
