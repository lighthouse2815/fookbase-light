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
        IdentityInputValidator.ValidateText(phoneNumber, 32, nameof(phoneNumber));
        IdentityInputValidator.ValidateSha256Hex(codeHash, "Mã băm OTP", nameof(codeHash));

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

    public int SendCount { get; private set; }

    public int FailedAttemptCount { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset NextResendAllowedAtUtc { get; private set; }

    public DateTimeOffset SendLimitWindowStartedAtUtc { get; private set; }

    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public bool IsUsableAt(DateTimeOffset now) =>
        ConsumedAtUtc is null && FailedAttemptCount < IdentityModuleConstants.Challenges.MaximumFailedAttempts && ExpiresAtUtc > now;

    public bool TryResend(string codeHash, DateTimeOffset now)
    {
        if (ConsumedAtUtc is not null || NextResendAllowedAtUtc > now || !IdentityInputValidator.IsValidSha256Hex(codeHash)) return false;

        (SendLimitWindowStartedAtUtc, SendCount) = IdentityChallengeWindow.ResetIfExpired(
            SendLimitWindowStartedAtUtc,
            SendCount,
            now);

        if (SendCount >= IdentityModuleConstants.Challenges.MaximumSendsPerWindow) return false;

        CodeHash = codeHash;
        ExpiresAtUtc = now.AddMinutes(10);
        NextResendAllowedAtUtc = now.AddMinutes(1);
        FailedAttemptCount = 0;
        SendCount++;
        return true;
    }
}
