using System.Globalization;
using System.Text;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Domain.Enums;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Users.Domain.Enums;
using Fookbase.Api.Modules.Users.Services;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class RegistrationChallengeService(
    UserManager<User> userManager,
    FookbaseDbContext dbContext,
    OtpService otpService,
    UserProfileService userProfileService,
    UserPrivacySettingsService privacySettingsService,
    AuthenticationService authenticationService,
    TimeProvider timeProvider)
{
    public async Task<RegistrationChallengeResponse> StartAsync(
        RegistrationStartRequest request,
        CancellationToken cancellationToken = default)
    {
        var input = await ValidateStartAsync(request, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var candidate = new User(Guid.NewGuid(), input.Contact.Kind == ContactKind.Email ? input.Contact.Value : null, "pending", now);
        var passwordHash = userManager.PasswordHasher.HashPassword(candidate, request.Password!);
        var challenge = await otpService.StartRegistrationAsync(
            input.Contact, passwordHash, input.FirstName, input.LastName, input.DateOfBirth, input.Gender, now, cancellationToken);

        return ToResponse(challenge);
    }

    public async Task<RegistrationChallengeResponse> ResendAsync(
        RegistrationResendRequest request,
        CancellationToken cancellationToken = default) =>
        ToResponse(await otpService.ResendRegistrationAsync(request.ChallengeId, cancellationToken));

    public async Task<AuthenticationResponse> VerifyAsync(
        RegistrationVerifyRequest request,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var (challenge, now) = await otpService.VerifyRegistrationAsync(request.ChallengeId, request.Code, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await otpService.ConsumeRegistrationAsync(challenge, request.Code!, now, cancellationToken);

        var contact = new ContactIdentifier(challenge.ContactKind, challenge.Contact);
        if (await ContactExistsAsync(contact, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            throw Conflict("duplicate_contact", "An account with this contact already exists.");
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
            throw new BusinessException(new ApplicationError(
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

        await transaction.CommitAsync(cancellationToken);
        return issued;
    }

    private async Task<StartInput> ValidateStartAsync(
        RegistrationStartRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var contact = ContactIdentifier.Parse(request.Contact!);
        if (string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Trim().Length > 50) errors["firstName"] = ["First name must contain between 1 and 50 characters."];
        if (string.IsNullOrWhiteSpace(request.LastName) || request.LastName.Trim().Length > 50) errors["lastName"] = ["Last name must contain between 1 and 50 characters."];
        if (request.DateOfBirth is null || !IsOldEnough(request.DateOfBirth.Value)) errors["dateOfBirth"] = ["You must be at least 13 years old."];
        if (!TryParseGender(request.Gender, out var gender)) errors["gender"] = ["Gender is invalid."];
        if (string.IsNullOrWhiteSpace(request.Password)) errors["password"] = ["Password is required."];

        if (errors.Count > 0)
        {
            throw Validation(errors);
        }

        var candidate = new User(Guid.NewGuid(), contact.Kind == ContactKind.Email ? contact.Value : null, "pending", timeProvider.GetUtcNow());
        foreach (var validator in userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(userManager, candidate, request.Password!);
            if (!result.Succeeded)
            {
                errors["password"] = result.Errors.Select(error => error.Description).ToArray();
                break;
            }
        }

        if (errors.Count > 0) throw Validation(errors);
        if (await ContactExistsAsync(contact, cancellationToken))
        {
            throw Conflict("duplicate_contact", "An account with this contact already exists.");
        }

        return new StartInput(
            contact,
            request.FirstName!.Trim(),
            request.LastName!.Trim(),
            request.DateOfBirth!.Value,
            gender);
    }

    private async Task<bool> ContactExistsAsync(ContactIdentifier contact, CancellationToken cancellationToken) =>
        contact.Kind == ContactKind.Email
            ? await userManager.FindByEmailAsync(contact.Value) is not null
            : await dbContext.Users.AnyAsync(user => user.PhoneNumber == contact.Value, cancellationToken);

    private bool IsOldEnough(DateOnly dateOfBirth)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return dateOfBirth <= today && dateOfBirth <= today.AddYears(-IdentityModuleConstants.Challenges.MinimumRegistrationAge);
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

    private static RegistrationChallengeResponse ToResponse(RegistrationChallenge challenge) =>
        new(challenge.Id, challenge.ExpiresAtUtc, challenge.NextResendAllowedAtUtc);

    private static BusinessException Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new BusinessException(new ApplicationError(ErrorCode.ValidationFailed, ErrorCode.ValidationFailed.Message, ApplicationErrorType.Validation, errors));

    private static BusinessException Conflict(string code, string message) =>
        new BusinessException(new ApplicationError(code, message, ApplicationErrorType.Conflict));

    private sealed record StartInput(
        ContactIdentifier Contact,
        string FirstName,
        string LastName,
        DateOnly DateOfBirth,
        Gender Gender);
}
