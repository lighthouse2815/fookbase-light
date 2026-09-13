namespace Fookbase.Api.Modules.Identity.Entities;

public sealed class TwoFactorLoginChallenge
{
    private TwoFactorLoginChallenge() { }
    private TwoFactorLoginChallenge(Guid id, Guid userId, DateTimeOffset now) { Id = id; UserId = userId; CreatedAtUtc = now; ExpiresAtUtc = now.AddMinutes(5); }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }
    public static TwoFactorLoginChallenge Create(Guid userId, DateTimeOffset now) => new(Guid.NewGuid(), userId, now);
    public bool IsUsableAt(DateTimeOffset now) => ConsumedAtUtc is null && ExpiresAtUtc > now;
    public void Consume(DateTimeOffset now) => ConsumedAtUtc = now;
}
