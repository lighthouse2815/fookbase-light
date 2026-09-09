using Fookbase.Api.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Media.Config;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.HasIndex(x => new { x.ProcessedAtUtc, x.OccurredAtUtc });
    }
}
