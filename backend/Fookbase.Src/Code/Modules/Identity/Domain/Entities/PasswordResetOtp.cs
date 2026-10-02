using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Identity.Entities;

[Index(nameof(UserId), IsUnique = true)]
public sealed class PasswordResetOtp
{
    private PasswordResetOtp(){}

    public PasswordResetOtp(Guid userId, string phoneNumber, string codeHash, DateTimeOffset now)
    {
        if (userId == Guid.Empty) throw new ArgumentException("The user identifier is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber.Length > 32) throw new ArgumentException("The phone number is invalid.", nameof(phoneNumber));
        if (!IsValidCodeHash(codeHash)) throw new ArgumentException("The OTP hash must be a SHA-256 hexadecimal digest.", nameof(codeHash));

        Id = Guid.NewGuid();
        UserId = userId;
        PhoneNumber = phoneNumber;
        CodeHash = codeHash;
        CreatedAtUtc = now;
        ExpiresAtUtc = now.AddMinutes(10);
        NextResendAllowedAtUtc = now.AddMinutes(1);
        SendLimitWindowStartedAtUtc = now;
        SendCount = 1;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Cascade)]
    public User User { get; private set; } = null!;

    [MaxLength(32)]
    public string PhoneNumber { get; private set; } = string.Empty;

    [MaxLength(64)]
    public string CodeHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset NextResendAllowedAtUtc { get; private set; }
    public DateTimeOffset SendLimitWindowStartedAtUtc { get; private set; }
    public int SendCount { get; private set; }
    public int FailedAttemptCount { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public bool IsUsableAt(DateTimeOffset now) =>
        ConsumedAtUtc is null && FailedAttemptCount < IdentityModuleConstants.Challenges.MaximumFailedAttempts && ExpiresAtUtc > now;

    public bool TryResend(string codeHash, DateTimeOffset now)
    {
        if (ConsumedAtUtc is not null || NextResendAllowedAtUtc > now || !IsValidCodeHash(codeHash)) return false;
        if (SendLimitWindowStartedAtUtc.AddHours(1) <= now)
        {
            SendLimitWindowStartedAtUtc = now;
            SendCount = 0;
        }
        if (SendCount >= IdentityModuleConstants.Challenges.MaximumSendsPerWindow) return false;

        CodeHash = codeHash;
        ExpiresAtUtc = now.AddMinutes(10);
        NextResendAllowedAtUtc = now.AddMinutes(1);
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
