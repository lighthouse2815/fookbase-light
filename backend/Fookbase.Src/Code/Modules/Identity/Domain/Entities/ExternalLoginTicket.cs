using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Identity.Entities;

[Index(nameof(CodeHash), nameof(ExpiresAtUtc), IsUnique = true)]
public sealed class ExternalLoginTicket
{
    private ExternalLoginTicket(){}

    public ExternalLoginTicket(
        string codeHash,
        ExternalLoginTicketPurpose purpose,
        string client,
        string provider,
        string providerKey,
        string email,
        Guid userId,
        DateTimeOffset now)
    {
        IdentityInputValidator.ValidateSha256Hex(codeHash, "Mã băm của mã hoàn tất", nameof(codeHash));
        IdentityInputValidator.ValidateText(client, 32, nameof(client));
        IdentityInputValidator.ValidateText(provider, 32, nameof(provider));
        IdentityInputValidator.ValidateText(providerKey, 256, nameof(providerKey));
        IdentityInputValidator.ValidateText(email, 256, nameof(email));

        Id = Guid.NewGuid();
        CodeHash = codeHash;
        Purpose = purpose;
        Client = client;
        Provider = provider;
        ProviderKey = providerKey;
        Email = email;
        UserId = userId;
        CreatedAtUtc = now;
        ExpiresAtUtc = now.AddMinutes(5);
    }

    public Guid Id { get; private set; }

    [MaxLength(64)]
    public string CodeHash { get; private set; } = string.Empty;

    public ExternalLoginTicketPurpose Purpose { get; private set; }

    [MaxLength(32)]
    public string Client { get; private set; } = string.Empty;

    [MaxLength(32)]
    public string Provider { get; private set; } = string.Empty;

    [MaxLength(256)]
    public string ProviderKey { get; private set; } = string.Empty;

    [MaxLength(256)]
    public string Email { get; private set; } = string.Empty;

    public Guid UserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Cascade)]
    public User User { get; private set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public bool IsUsableAt(DateTimeOffset now) => ConsumedAtUtc is null && ExpiresAtUtc > now;

    public bool TryConsumeAt(DateTimeOffset now)
    {
        if (!IsUsableAt(now))
        {
            return false;
        }

        ConsumedAtUtc = now;
        return true;
    }

}
