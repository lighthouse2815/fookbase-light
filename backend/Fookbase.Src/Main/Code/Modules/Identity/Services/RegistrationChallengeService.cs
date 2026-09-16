using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Users.Entities;
using Fookbase.Api.Modules.Users.Services;
using Fookbase.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class RegistrationChallengeService(
    UserManager<User> userManager,
    FookbaseDbContext dbContext,
    IContactOtpSender contactOtpSender,
    UserProfileService userProfileService,
    UserPrivacySettingsService privacySettingsService,
    AuthenticationService authenticationService,
    TimeProvider timeProvider)
{
    private const int MinimumAge = 13;

    public async Task<ApplicationResult<RegistrationChallengeResponse>> StartAsync(
        RegistrationStartRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateStartAsync(request, cancellationToken);
        if (!validation.Succeeded) return ApplicationResult<RegistrationChallengeResponse>.Failure(validation.Error!);

        var input = validation.Value!;
        var now = timeProvider.GetUtcNow();
        var code = CreateCode();
        var codeHash = Hash(code);
        var candidate = new User(Guid.NewGuid(), input.Contact.Kind == ContactKind.Email ? input.Contact.Value : null, "pending", now);
        var passwordHash = userManager.PasswordHasher.HashPassword(candidate, request.Password!);
        var challenge = await dbContext.RegistrationChallenges.SingleOrDefaultAsync(
            item => item.Contact == input.Contact.Value,
            cancellationToken);

        if (challenge is null)
        {
            challenge = RegistrationChallenge.Create(
                input.Contact,
                codeHash,
                passwordHash,
                input.FirstName,
                input.LastName,
                input.DateOfBirth,
                input.Gender,
                now);
            dbContext.RegistrationChallenges.Add(challenge);
        }
        else if (!challenge.TryRestart(
                     codeHash,
                     passwordHash,
                     input.FirstName,
                     input.LastName,
                     input.DateOfBirth,
                     input.Gender,
                     now))
        {
            return Conflict<RegistrationChallengeResponse>("registration_send_limit", "Too many verification codes were requested.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        try
        {
            await contactOtpSender.SendAsync(input.Contact, code, cancellationToken);
        }
        catch
        {
            dbContext.RegistrationChallenges.Remove(challenge);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Unavailable<RegistrationChallengeResponse>(input.Contact.Kind);
        }

        return ApplicationResult<RegistrationChallengeResponse>.Success(ToResponse(challenge));
    }

    public async Task<ApplicationResult<RegistrationChallengeResponse>> ResendAsync(
        RegistrationResendRequest request,
        CancellationToken cancellationToken = default)
    {
        var challenge = await dbContext.RegistrationChallenges.SingleOrDefaultAsync(
            item => item.Id == request.ChallengeId,
            cancellationToken);
        if (challenge is null) return InvalidCode<RegistrationChallengeResponse>();

        var now = timeProvider.GetUtcNow();
        var code = CreateCode();
        if (!challenge.TryResend(Hash(code), now))
        {
            return Conflict<RegistrationChallengeResponse>("registration_resend_unavailable", "A new verification code cannot be sent yet.");
        }

        try
        {
            await contactOtpSender.SendAsync(new ContactIdentifier(challenge.ContactKind, challenge.Contact), code, cancellationToken);
        }
        catch
        {
            dbContext.RegistrationChallenges.Remove(challenge);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Unavailable<RegistrationChallengeResponse>(challenge.ContactKind);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult<RegistrationChallengeResponse>.Success(ToResponse(challenge));
    }

    public async Task<ApplicationResult<AuthenticationResponse>> VerifyAsync(
        RegistrationVerifyRequest request,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (request.ChallengeId == Guid.Empty || string.IsNullOrWhiteSpace(request.Code) || request.Code.Length != 6 ||
            !request.Code.All(char.IsAsciiDigit))
        {
            return InvalidCode<AuthenticationResponse>();
        }

        var challenge = await dbContext.RegistrationChallenges.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == request.ChallengeId,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (challenge is null || !challenge.IsUsableAt(now)) return InvalidCode<AuthenticationResponse>();

        var suppliedHash = Hash(request.Code);
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(challenge.CodeHash),
                Convert.FromHexString(suppliedHash)))
        {
            var attempts = await dbContext.RegistrationChallenges
                .Where(item => item.Id == challenge.Id && item.ConsumedAtUtc == null && item.ExpiresAtUtc > now && item.FailedAttemptCount < 5)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.FailedAttemptCount, item => item.FailedAttemptCount + 1), cancellationToken);
            return InvalidCode<AuthenticationResponse>();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var consumed = await dbContext.RegistrationChallenges
            .Where(item => item.Id == challenge.Id && item.ConsumedAtUtc == null && item.ExpiresAtUtc > now &&
                item.FailedAttemptCount < 5 && item.CodeHash == suppliedHash)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ConsumedAtUtc, now), cancellationToken);
        if (consumed != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return InvalidCode<AuthenticationResponse>();
        }

        var contact = new ContactIdentifier(challenge.ContactKind, challenge.Contact);
        if (await ContactExistsAsync(contact, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict<AuthenticationResponse>("duplicate_contact", "An account with this contact already exists.");
        }

        var username = await GenerateUsernameAsync(challenge.FirstName, challenge.LastName, cancellationToken);
        var user = new User(
            Guid.NewGuid(),
            contact.Kind == ContactKind.Email ? contact.Value : null,
            username,
            now)
        {
            PasswordHash = challenge.PasswordHash,
            EmailConfirmed = contact.Kind == ContactKind.Email,
            PhoneNumber = contact.Kind == ContactKind.Phone ? contact.Value : null,
            PhoneNumberConfirmed = contact.Kind == ContactKind.Phone
        };
        var create = await userManager.CreateAsync(user);
        if (!create.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ApplicationResult<AuthenticationResponse>.Failure(new ApplicationError(
                "registration_creation_failed",
                "The account could not be created.",
                ApplicationErrorType.Validation,
                create.Errors.GroupBy(error => error.Code).ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray())));
        }

        await userProfileService.EnsureCreatedAsync(
            user.Id,
            username,
            $"{challenge.FirstName} {challenge.LastName}",
            challenge.DateOfBirth,
            challenge.Gender,
            cancellationToken);
        await privacySettingsService.EnsureCreatedAsync(user.Id, cancellationToken);
        var issued = await authenticationService.IssueSessionAsync(user, userAgent, cancellationToken);
        if (!issued.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return issued;
        }

        await transaction.CommitAsync(cancellationToken);
        return issued;
    }

    private async Task<ApplicationResult<StartInput>> ValidateStartAsync(
        RegistrationStartRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (!ContactIdentifier.TryParse(request.Contact, out var contact)) errors["contact"] = ["Email or Vietnamese mobile number is invalid."];
        if (string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Trim().Length > 50) errors["firstName"] = ["First name must contain between 1 and 50 characters."];
        if (string.IsNullOrWhiteSpace(request.LastName) || request.LastName.Trim().Length > 50) errors["lastName"] = ["Last name must contain between 1 and 50 characters."];
        if (request.DateOfBirth is null || !IsOldEnough(request.DateOfBirth.Value)) errors["dateOfBirth"] = ["You must be at least 13 years old."];
        if (!TryParseGender(request.Gender, out var gender)) errors["gender"] = ["Gender is invalid."];
        if (string.IsNullOrWhiteSpace(request.Password)) errors["password"] = ["Password is required."];

        if (errors.Count > 0)
        {
            return Validation<StartInput>(errors);
        }

        var candidate = new User(Guid.NewGuid(), contact!.Kind == ContactKind.Email ? contact.Value : null, "pending", timeProvider.GetUtcNow());
        foreach (var validator in userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(userManager, candidate, request.Password!);
            if (!result.Succeeded)
            {
                errors["password"] = result.Errors.Select(error => error.Description).ToArray();
                break;
            }
        }

        if (errors.Count > 0) return Validation<StartInput>(errors);
        if (await ContactExistsAsync(contact!, cancellationToken))
        {
            return Conflict<StartInput>("duplicate_contact", "An account with this contact already exists.");
        }

        return ApplicationResult<StartInput>.Success(new StartInput(
            contact!,
            request.FirstName!.Trim(),
            request.LastName!.Trim(),
            request.DateOfBirth!.Value,
            gender));
    }

    private async Task<bool> ContactExistsAsync(ContactIdentifier contact, CancellationToken cancellationToken) =>
        contact.Kind == ContactKind.Email
            ? await userManager.FindByEmailAsync(contact.Value) is not null
            : await dbContext.Users.AnyAsync(user => user.PhoneNumber == contact.Value, cancellationToken);

    private bool IsOldEnough(DateOnly dateOfBirth)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return dateOfBirth <= today && dateOfBirth <= today.AddYears(-MinimumAge);
    }

    private async Task<string> GenerateUsernameAsync(string firstName, string lastName, CancellationToken cancellationToken)
    {
        var baseUsername = SanitizeUsername($"{firstName}.{lastName}");
        for (var suffix = 1; ; suffix++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var suffixText = suffix == 1 ? string.Empty : $".{suffix}";
            var candidate = baseUsername[..Math.Min(baseUsername.Length, 32 - suffixText.Length)] + suffixText;
            if (await userManager.FindByNameAsync(candidate) is null) return candidate;
        }
    }

    private static string SanitizeUsername(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsAsciiLetterOrDigit(character)) builder.Append(char.ToLowerInvariant(character));
            else if (builder.Length > 0 && builder[^1] != '.') builder.Append('.');
        }

        var result = builder.ToString().Trim('.');
        return result.Length < 3 ? "user" : result[..Math.Min(32, result.Length)];
    }

    private static bool TryParseGender(string? value, out Gender gender) =>
        Enum.TryParse(value, true, out gender) && Enum.IsDefined(gender);

    private static string CreateCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static RegistrationChallengeResponse ToResponse(RegistrationChallenge challenge) =>
        new(challenge.Id, challenge.ExpiresAtUtc, challenge.ResendAvailableAtUtc);

    private static ApplicationResult<T> Validation<T>(IReadOnlyDictionary<string, string[]> errors) =>
        ApplicationResult<T>.Failure(new ApplicationError("validation_failed", "One or more validation errors occurred.", ApplicationErrorType.Validation, errors));

    private static ApplicationResult<T> InvalidCode<T>() =>
        ApplicationResult<T>.Failure(new ApplicationError("invalid_registration_code", "The verification code is invalid or expired.", ApplicationErrorType.Unauthorized));

    private static ApplicationResult<T> Conflict<T>(string code, string message) =>
        ApplicationResult<T>.Failure(new ApplicationError(code, message, ApplicationErrorType.Conflict));

    private static ApplicationResult<T> Unavailable<T>(ContactKind kind) =>
        ApplicationResult<T>.Failure(new ApplicationError(
            kind == ContactKind.Email ? "email_delivery_unavailable" : "sms_delivery_unavailable",
            "The verification code could not be delivered.",
            ApplicationErrorType.Conflict));

    private sealed record StartInput(
        ContactIdentifier Contact,
        string FirstName,
        string LastName,
        DateOnly DateOfBirth,
        Gender Gender);
}
