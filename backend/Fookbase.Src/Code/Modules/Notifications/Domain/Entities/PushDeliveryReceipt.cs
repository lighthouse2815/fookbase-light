using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Notifications.Entities;

[Table("PushDeliveryReceipts")]
[Index(nameof(ExpoReceiptId), IsUnique = true)]
[Index(nameof(CheckedAtUtc), nameof(AvailableAtUtc))]
public sealed class PushDeliveryReceipt
{
    public const int MaximumExpoReceiptIdLength = 64;

    private PushDeliveryReceipt()
    {
    }

    public PushDeliveryReceipt(Guid id, Guid pushDeviceId, string expoReceiptId, DateTimeOffset availableAtUtc)
    {
        Id = id;
        PushDeviceId = pushDeviceId;
        ExpoReceiptId = expoReceiptId;
        AvailableAtUtc = availableAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }

    public Guid PushDeviceId { get; private set; }

    [Required]
    [MaxLength(MaximumExpoReceiptIdLength)]
    public string ExpoReceiptId { get; private set; } = string.Empty;

    public DateTimeOffset AvailableAtUtc { get; private set; }

    public DateTimeOffset? CheckedAtUtc { get; private set; }

    public int CheckAttempts { get; private set; }

    [ForeignKey(nameof(PushDeviceId))]
    [InverseProperty(nameof(PushDevice.DeliveryReceipts))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public PushDevice PushDevice { get; private set; } = null!;

    public void MarkChecked(DateTimeOffset checkedAtUtc) => CheckedAtUtc ??= checkedAtUtc;

    public void RetryAt(DateTimeOffset availableAtUtc)
    {
        CheckAttempts++;
        AvailableAtUtc = availableAtUtc;
    }
}
