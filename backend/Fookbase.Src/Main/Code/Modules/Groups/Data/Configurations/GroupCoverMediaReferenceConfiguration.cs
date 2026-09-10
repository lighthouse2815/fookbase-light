using Fookbase.Api.Modules.Groups.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Groups.Data.Configurations;

internal sealed class GroupCoverMediaReferenceConfiguration
    : IEntityTypeConfiguration<GroupCoverMediaReference>
{
    public void Configure(EntityTypeBuilder<GroupCoverMediaReference> builder)
    {
        builder.ToTable("GroupCoverMediaReferences");
        builder.HasKey(reference => reference.GroupId);
        builder.HasIndex(reference => reference.MediaId);
        builder.Property(reference => reference.AttachedAtUtc).IsRequired();
    }
}
