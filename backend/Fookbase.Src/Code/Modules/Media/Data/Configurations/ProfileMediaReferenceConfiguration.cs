using Fookbase.Api.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Media.Data.Configurations;

internal sealed class ProfileMediaReferenceConfiguration : IEntityTypeConfiguration<ProfileMediaReference>
{
    public void Configure(EntityTypeBuilder<ProfileMediaReference> builder)
    {
        builder.ToTable("ProfileMediaReferences");
        builder.HasKey(reference => new { reference.UserId, reference.Slot });
        builder.HasIndex(reference => reference.MediaId);
        builder.Property(reference => reference.AttachedAtUtc).IsRequired();
    }
}
