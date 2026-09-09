using Fookbase.Api.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Media.Config;

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages"); builder.HasKey(x => x.EventId);
        builder.Property(x => x.EventType).HasMaxLength(200).IsRequired();
    }
}
