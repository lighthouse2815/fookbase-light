using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Common;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Identity.Entities;

[Table("PasswordResetChallenges")]
[Index(nameof(UserId), IsUnique = true)]
public sealed class PasswordResetOtp
{
    private PasswordResetOtp()
    {
    }

    private PasswordResetOtp(Guid userId, string contact, string codeHash, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Contact = contact;
        CodeHash = codeHash;
        CreatedAtUtc = now;
        ExpiresAtUtc = now.AddMinutes(10);
        ResendAvailableAtUtc = now.AddMinutes(1);
        WindowStartedAtUtc = now;
        SendCount = 1;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    [MaxLength(32)]
    public string Contact { get; private set; } = string.Empty;

    [MaxLength(64)]
    public string CodeHash { get; private set; } = string.Empty;

    [DeleteBehavior(DeleteBehavior.Cascade)]
    public User User { get; private set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset ResendAvailableAtUtc { get; private set; }
    public DateTimeOffset WindowStartedAtUtc { get; private set; }
    public int SendCount { get; private set; }
    public int FailedAttemptCount { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public static PasswordResetOtp Create(Guid userId, string contact, string codeHash, DateTimeOffset now)
    {
        if (userId == Guid.Empty) throw new ArgumentException("The user identifier is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(contact) || contact.Length > 32) throw new ArgumentException("The phone number is invalid.", nameof(contact));
        if (!IsValidCodeHash(codeHash)) throw new ArgumentException("The OTP hash must be a SHA-256 hexadecimal digest.", nameof(codeHash));
        return new PasswordResetOtp(userId, contact, codeHash, now);
    }

    public bool IsUsableAt(DateTimeOffset now) =>
        ConsumedAtUtc is null && FailedAttemptCount < IdentityModuleConstants.Challenges.MaximumFailedAttempts && ExpiresAtUtc > now;

    public bool TryResend(string codeHash, DateTimeOffset now)
    {
        if (ConsumedAtUtc is not null || ResendAvailableAtUtc > now || !IsValidCodeHash(codeHash)) return false;
        if (WindowStartedAtUtc.AddHours(1) <= now)
        {
            WindowStartedAtUtc = now;
            SendCount = 0;
        }
        if (SendCount >= IdentityModuleConstants.Challenges.MaximumSendsPerWindow) return false;

        CodeHash = codeHash;
        ExpiresAtUtc = now.AddMinutes(10);
        ResendAvailableAtUtc = now.AddMinutes(1);
        FailedAttemptCount = 0;
        SendCount++;
        return true;
    }

    public void ReplaceCodeHash(string codeHash)
    {
        if (!IsValidCodeHash(codeHash)) throw new ArgumentException("The OTP hash must be a SHA-256 hexadecimal digest.", nameof(codeHash));
        CodeHash = codeHash;
    }

    private static bool IsValidCodeHash(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
}
