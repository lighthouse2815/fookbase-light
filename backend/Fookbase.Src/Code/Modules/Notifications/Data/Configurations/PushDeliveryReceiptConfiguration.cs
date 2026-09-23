using Fookbase.Api.Modules.Notifications.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Notifications.Data.Configurations;

internal sealed class PushDeliveryReceiptConfiguration : IEntityTypeConfiguration<PushDeliveryReceipt>
{
    public void Configure(EntityTypeBuilder<PushDeliveryReceipt> builder)
    {
        builder.ToTable("PushDeliveryReceipts");
        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.ExpoReceiptId).HasMaxLength(64).IsRequired();
        builder.Property(receipt => receipt.AvailableAtUtc).IsRequired();
        builder.HasIndex(receipt => receipt.ExpoReceiptId).IsUnique();
        builder.HasIndex(receipt => new { receipt.CheckedAtUtc, receipt.AvailableAtUtc });
        builder.HasOne<PushDevice>().WithMany().HasForeignKey(receipt => receipt.PushDeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
