using Fookbase.Api.Modules.Groups.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Groups.Data.Configurations;

internal sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.HasIndex(group => new { group.Privacy, group.Name })
            .HasFilter("\"DeletedAtUtc\" IS NULL");
        builder.HasIndex(group => new { group.OwnerUserId, group.CreatedAtUtc })
            .HasFilter("\"DeletedAtUtc\" IS NULL");
    }
}
