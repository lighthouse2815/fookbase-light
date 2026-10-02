using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Domain.Enums;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Identity.Entities;

[Table("RegistrationChallenges")]
[Index(nameof(Contact), IsUnique = true)]
public sealed class RegistrationChallenge
{
    private RegistrationChallenge()
    {
    }

    public RegistrationChallenge(
        ContactIdentifier contact,
        string codeHash,
        string passwordHash,
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        Gender gender,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(contact);
        IdentityInputValidator.ValidateText(contact.Value, 256, nameof(contact));
        IdentityInputValidator.ValidateSha256Hex(codeHash, "Mã băm OTP", nameof(codeHash));
        IdentityInputValidator.ValidateText(passwordHash, 512, nameof(passwordHash));

        var normalizedFirstName = firstName?.Trim() ?? string.Empty;
        var normalizedLastName = lastName?.Trim() ?? string.Empty;
        IdentityInputValidator.ValidateText(normalizedFirstName, 50, nameof(firstName));
        IdentityInputValidator.ValidateText(normalizedLastName, 50, nameof(lastName));

        Id = Guid.NewGuid();
        ContactKind = contact.Kind;
        Contact = contact.Value;
        CodeHash = codeHash;
        PasswordHash = passwordHash;
        FirstName = normalizedFirstName;
        LastName = normalizedLastName;
        DateOfBirth = dateOfBirth;
        Gender = gender;
        CreatedAtUtc = now;
        ExpiresAtUtc = now.AddMinutes(10);
        NextResendAllowedAtUtc = now.AddMinutes(1);
        SendLimitWindowStartedAtUtc = now;
        SendCount = 1;
    }

    public Guid Id { get; private set; }
    public ContactKind ContactKind { get; private set; }

    [MaxLength(256)]
    public string Contact { get; private set; } = string.Empty;

    [MaxLength(64)]
    public string CodeHash { get; private set; } = string.Empty;

    [MaxLength(512)]
    public string PasswordHash { get; private set; } = string.Empty;

    [MaxLength(50)]
    public string FirstName { get; private set; } = string.Empty;

    [MaxLength(50)]
    public string LastName { get; private set; } = string.Empty;
    public DateOnly DateOfBirth { get; private set; }
    public Gender Gender { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset NextResendAllowedAtUtc { get; private set; }
    public DateTimeOffset SendLimitWindowStartedAtUtc { get; private set; }
    public int SendCount { get; private set; }
    public int FailedAttemptCount { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public bool IsUsableAt(DateTimeOffset now) =>
        ConsumedAtUtc is null && FailedAttemptCount < IdentityModuleConstants.Challenges.MaximumFailedAttempts && ExpiresAtUtc > now;

    public void RegisterFailedAttempt(DateTimeOffset now)
    {
        if (IsUsableAt(now)) FailedAttemptCount++;
    }

    public bool TryConsumeAt(DateTimeOffset now)
    {
        if (!IsUsableAt(now)) return false;
        ConsumedAtUtc = now;
        return true;
    }

    public bool TryRestart(
        string codeHash,
        string passwordHash,
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        Gender gender,
        DateTimeOffset now)
    {
        if (ConsumedAtUtc is not null || NextResendAllowedAtUtc > now || !IdentityInputValidator.IsValidSha256Hex(codeHash) || string.IsNullOrWhiteSpace(passwordHash) ||
            string.IsNullOrWhiteSpace(firstName) || firstName.Trim().Length > 50 ||
            string.IsNullOrWhiteSpace(lastName) || lastName.Trim().Length > 50)
        {
            return false;
        }

        (SendLimitWindowStartedAtUtc, SendCount) = IdentityChallengeWindow.ResetIfExpired(
            SendLimitWindowStartedAtUtc,
            SendCount,
            now);
        if (SendCount >= IdentityModuleConstants.Challenges.MaximumSendsPerWindow) return false;

        CodeHash = codeHash;
        PasswordHash = passwordHash;
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        DateOfBirth = dateOfBirth;
        Gender = gender;
        ExpiresAtUtc = now.AddMinutes(10);
        NextResendAllowedAtUtc = now.AddMinutes(1);
        FailedAttemptCount = 0;
        SendCount++;
        return true;
    }

    public bool TryResend(string codeHash, DateTimeOffset now)
    {
        if (ConsumedAtUtc is not null || ExpiresAtUtc <= now || NextResendAllowedAtUtc > now || !IdentityInputValidator.IsValidSha256Hex(codeHash))
        {
            return false;
        }

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

    public void ReplaceCodeHash(string codeHash)
    {
        IdentityInputValidator.ValidateSha256Hex(codeHash, "Mã băm OTP", nameof(codeHash));
        CodeHash = codeHash;
    }

}
