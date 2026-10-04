namespace Fookbase.Api.Modules.Notifications.Entities;

public sealed class PushDeliveryReceipt
{
    private PushDeliveryReceipt()
    {
    }

    private PushDeliveryReceipt(Guid id, Guid pushDeviceId, string expoReceiptId, DateTimeOffset availableAtUtc)
    {
        Id = id;
        PushDeviceId = pushDeviceId;
        ExpoReceiptId = expoReceiptId;
        AvailableAtUtc = availableAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid PushDeviceId { get; private set; }

    public string ExpoReceiptId { get; private set; } = string.Empty;

    public DateTimeOffset AvailableAtUtc { get; private set; }

    public DateTimeOffset? CheckedAtUtc { get; private set; }

    public int CheckAttempts { get; private set; }

    public static PushDeliveryReceipt Create(Guid id, Guid pushDeviceId, string expoReceiptId, DateTimeOffset availableAtUtc) =>
        new(id, pushDeviceId, expoReceiptId, availableAtUtc);

    public void MarkChecked(DateTimeOffset checkedAtUtc) => CheckedAtUtc ??= checkedAtUtc;

    public void RetryAt(DateTimeOffset availableAtUtc)
    {
        CheckAttempts++;
        AvailableAtUtc = availableAtUtc;
    }
}
