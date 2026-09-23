namespace Fookbase.Api.Modules.Notifications.Entities;

public sealed class PushDevice
{
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

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string ExpoPushToken { get; private set; } = string.Empty;

    public DateTimeOffset RegisteredAtUtc { get; private set; }

    public DateTimeOffset? DisabledAtUtc { get; private set; }

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
