using Fookbase.Api.Modules.Notifications.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Notifications.Data.Configurations;

internal sealed class PushDeviceConfiguration : IEntityTypeConfiguration<PushDevice>
{
    public void Configure(EntityTypeBuilder<PushDevice> builder)
    {
        builder.ToTable("PushDevices");
        builder.HasKey(device => device.Id);
        builder.Property(device => device.ExpoPushToken).HasMaxLength(255).IsRequired();
        builder.Property(device => device.RegisteredAtUtc).IsRequired();
        builder.HasIndex(device => device.ExpoPushToken).IsUnique();
        builder.HasIndex(device => new { device.UserId, device.DisabledAtUtc });
    }
}
