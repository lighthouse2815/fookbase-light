using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Identity.Entities;

[Index(nameof(UserId), nameof(ExpiresAtUtc))]
public sealed class TwoFactorLoginChallenge
{
    private TwoFactorLoginChallenge() { }

    public TwoFactorLoginChallenge(
        Guid userId,
        DateTimeOffset now,
        string? pendingExternalProvider = null,
        string? pendingExternalProviderKey = null)
    {
        IdentityInputValidator.ValidateOptionalExternalLogin(pendingExternalProvider, pendingExternalProviderKey);

        Id = Guid.NewGuid();
        UserId = userId;
        CreatedAtUtc = now;
        ExpiresAtUtc = now.AddMinutes(5);
        PendingExternalProvider = pendingExternalProvider;
        PendingExternalProviderKey = pendingExternalProviderKey;
    }
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Cascade)]
    public User User { get; private set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    [MaxLength(32)]
    public string? PendingExternalProvider { get; private set; }

    [MaxLength(256)]
    public string? PendingExternalProviderKey { get; private set; }

    public bool IsUsableAt(DateTimeOffset now) => ConsumedAtUtc is null && ExpiresAtUtc > now;
    public void Consume(DateTimeOffset now) => ConsumedAtUtc = now;
}
