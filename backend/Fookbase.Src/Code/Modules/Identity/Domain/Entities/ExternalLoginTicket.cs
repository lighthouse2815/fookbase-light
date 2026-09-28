using Fookbase.Api.Modules.Identity.Domain.Enums;

namespace Fookbase.Api.Modules.Identity.Entities;

public sealed class ExternalLoginTicket
{
    private const int CodeHashLength = 64;
    private const int ProviderLength = 32;
    private const int ProviderKeyLength = 256;
    private const int EmailLength = 256;
    private const int ClientLength = 32;

    private ExternalLoginTicket()
    {
    }

    private ExternalLoginTicket(
        string codeHash,
        ExternalLoginTicketPurpose purpose,
        string client,
        string provider,
        string providerKey,
        string email,
        Guid? userId,
        DateTimeOffset now)
    {
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

    public string CodeHash { get; private set; } = string.Empty;

    public ExternalLoginTicketPurpose Purpose { get; private set; }

    public string Client { get; private set; } = string.Empty;

    public string Provider { get; private set; } = string.Empty;

    public string ProviderKey { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public Guid? UserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public static ExternalLoginTicket Create(
        string codeHash,
        ExternalLoginTicketPurpose purpose,
        string client,
        string provider,
        string providerKey,
        string email,
        Guid? userId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(codeHash) || codeHash.Length != CodeHashLength ||
            !codeHash.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("The completion code hash must be a SHA-256 hexadecimal digest.", nameof(codeHash));
        }

        ValidateText(client, ClientLength, nameof(client));
        ValidateText(provider, ProviderLength, nameof(provider));
        ValidateText(providerKey, ProviderKeyLength, nameof(providerKey));
        ValidateText(email, EmailLength, nameof(email));

        return new ExternalLoginTicket(
            codeHash,
            purpose,
            client,
            provider,
            providerKey,
            email,
            userId,
            now);
    }

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

    private static void ValidateText(string value, int maximumLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
        {
            throw new ArgumentException($"{name} must contain at most {maximumLength} characters.", name);
        }
    }
}
