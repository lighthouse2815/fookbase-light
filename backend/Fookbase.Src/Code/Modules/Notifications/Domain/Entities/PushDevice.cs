using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Notifications.Entities;

[Table("PushDevices")]
[Index(nameof(ExpoPushToken), IsUnique = true)]
[Index(nameof(UserId), nameof(DisabledAtUtc))]
public sealed class PushDevice
{
    public const int MaximumExpoPushTokenLength = 255;

    private PushDevice()
    {
    }

    private PushDevice(Guid id, Guid userId, string expoPushToken, DateTimeOffset registeredAtUtc)
    {
        Id = id;
        UserId = userId;
        ExpoPushToken = expoPushToken;
        RegisteredAtUtc = registeredAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    [Required]
    [MaxLength(MaximumExpoPushTokenLength)]
    public string ExpoPushToken { get; private set; } = string.Empty;

    public DateTimeOffset RegisteredAtUtc { get; private set; }

    public DateTimeOffset? DisabledAtUtc { get; private set; }

    [ForeignKey(nameof(UserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User User { get; private set; } = null!;

    public ICollection<PushDeliveryReceipt> DeliveryReceipts { get; private set; } = new List<PushDeliveryReceipt>();

    public static PushDevice Create(Guid id, Guid userId, string expoPushToken, DateTimeOffset registeredAtUtc) =>
        new(id, userId, expoPushToken, registeredAtUtc);

    public void Register(Guid userId, DateTimeOffset registeredAtUtc)
    {
        UserId = userId;
        RegisteredAtUtc = registeredAtUtc;
        DisabledAtUtc = null;
    }

    public void Disable(DateTimeOffset disabledAtUtc) => DisabledAtUtc ??= disabledAtUtc;
}
