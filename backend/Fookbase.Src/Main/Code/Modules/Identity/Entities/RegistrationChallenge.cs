using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Users.Entities;

namespace Fookbase.Api.Modules.Identity.Entities;

public sealed class RegistrationChallenge
{
    private const int MaximumFailedAttempts = 5;

    private RegistrationChallenge()
    {
    }

    private RegistrationChallenge(
        ContactIdentifier contact,
        string codeHash,
        string passwordHash,
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        Gender gender,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        ContactKind = contact.Kind;
        Contact = contact.Value;
        CodeHash = codeHash;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        DateOfBirth = dateOfBirth;
        Gender = gender;
        CreatedAtUtc = now;
        ExpiresAtUtc = now.AddMinutes(10);
        ResendAvailableAtUtc = now.AddMinutes(1);
        WindowStartedAtUtc = now;
        SendCount = 1;
    }

    public Guid Id { get; private set; }
    public ContactKind ContactKind { get; private set; }
    public string Contact { get; private set; } = string.Empty;
    public string CodeHash { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public DateOnly DateOfBirth { get; private set; }
    public Gender Gender { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset ResendAvailableAtUtc { get; private set; }
    public DateTimeOffset WindowStartedAtUtc { get; private set; }
    public int SendCount { get; private set; }
    public int FailedAttemptCount { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public static RegistrationChallenge Create(
        ContactIdentifier contact,
        string codeHash,
        string passwordHash,
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        Gender gender,
        DateTimeOffset now)
    {
        if (codeHash.Length != 64 || !codeHash.All(Uri.IsHexDigit))
            throw new ArgumentException("The OTP hash must be a SHA-256 hexadecimal digest.", nameof(codeHash));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("The password hash is required.", nameof(passwordHash));
        if (string.IsNullOrWhiteSpace(firstName) || firstName.Trim().Length > 50 ||
            string.IsNullOrWhiteSpace(lastName) || lastName.Trim().Length > 50)
            throw new ArgumentException("Names must contain between 1 and 50 characters.");

        return new RegistrationChallenge(contact, codeHash, passwordHash, firstName.Trim(), lastName.Trim(), dateOfBirth, gender, now);
    }

    public bool IsUsableAt(DateTimeOffset now) =>
        ConsumedAtUtc is null && FailedAttemptCount < MaximumFailedAttempts && ExpiresAtUtc > now;

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
}
