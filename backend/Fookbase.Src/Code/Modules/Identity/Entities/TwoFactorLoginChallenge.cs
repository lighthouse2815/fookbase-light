namespace Fookbase.Api.Modules.Identity.Entities;

public sealed class TwoFactorLoginChallenge
{
    private TwoFactorLoginChallenge() { }
    private TwoFactorLoginChallenge(
        Guid id,
        Guid userId,
        DateTimeOffset now,
        string? pendingExternalProvider,
        string? pendingExternalProviderKey)
    {
        Id = id;
        UserId = userId;
        CreatedAtUtc = now;
        ExpiresAtUtc = now.AddMinutes(5);
        PendingExternalProvider = pendingExternalProvider;
        PendingExternalProviderKey = pendingExternalProviderKey;
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }
    public string? PendingExternalProvider { get; private set; }
    public string? PendingExternalProviderKey { get; private set; }
    public static TwoFactorLoginChallenge Create(
        Guid userId,
        DateTimeOffset now,
        string? pendingExternalProvider = null,
        string? pendingExternalProviderKey = null)
    {
        if (string.IsNullOrWhiteSpace(pendingExternalProvider) != string.IsNullOrWhiteSpace(pendingExternalProviderKey))
        {
            throw new ArgumentException("A pending external provider and provider key must be supplied together.");
        }

        if (pendingExternalProvider is { Length: > 32 } || pendingExternalProviderKey is { Length: > 256 })
        {
            throw new ArgumentException("The pending external login is invalid.");
        }

        return new TwoFactorLoginChallenge(
            Guid.NewGuid(),
            userId,
            now,
            pendingExternalProvider,
            pendingExternalProviderKey);
    }
    public bool IsUsableAt(DateTimeOffset now) => ConsumedAtUtc is null && ExpiresAtUtc > now;
    public void Consume(DateTimeOffset now) => ConsumedAtUtc = now;
}
