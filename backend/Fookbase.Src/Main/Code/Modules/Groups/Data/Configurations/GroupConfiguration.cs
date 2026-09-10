using Fookbase.Api.Modules.Groups.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Groups.Data.Configurations;

internal sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("Groups");
        builder.HasKey(group => group.Id);
        builder.Property(group => group.Name).HasMaxLength(Group.MaximumNameLength).IsRequired();
        builder.Property(group => group.Description).HasMaxLength(Group.MaximumDescriptionLength);
        builder.Property(group => group.Privacy).HasConversion<int>().IsRequired();
        builder.Property(group => group.CreatedAtUtc).IsRequired();
        builder.HasIndex(group => new { group.Privacy, group.Name })
            .HasFilter("\"DeletedAtUtc\" IS NULL");
        builder.HasIndex(group => new { group.OwnerUserId, group.CreatedAtUtc })
            .HasFilter("\"DeletedAtUtc\" IS NULL");
    }
}
