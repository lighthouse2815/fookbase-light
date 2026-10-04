using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Fookbase.Api.Modules.Identity.Abstractions;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Domain.Enums;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Users.Domain.Enums;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class OtpService(
    FookbaseDbContext dbContext,
    IContactOtpSender contactOtpSender,
    TimeProvider timeProvider,
    ILogger<OtpService> logger)
{
    public async Task<RegistrationChallenge> StartRegistrationAsync(
        ContactIdentifier contact,
        string passwordHash,
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        Gender gender,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var code = CreateCode();
        var codeHash = Hash(code);
        var challenge = await dbContext.RegistrationChallenges.SingleOrDefaultAsync(
            item => item.Contact == contact.Value, cancellationToken);
        if (challenge is null)
        {
            challenge = new RegistrationChallenge(contact, codeHash, passwordHash, firstName, lastName, dateOfBirth, gender, now);
            dbContext.RegistrationChallenges.Add(challenge);
        }
        else if (!challenge.TryRestart(codeHash, passwordHash, firstName, lastName, dateOfBirth, gender, now))
        {
            throw new BusinessException(new ApplicationError(
                "registration_send_limit", "Too many verification codes were requested.", ApplicationErrorType.Conflict));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await SendRegistrationCodeAsync(challenge, code, cancellationToken);
        return challenge;
    }

    public async Task<RegistrationChallenge> ResendRegistrationAsync(
        Guid challengeId,
        CancellationToken cancellationToken = default)
    {
        var challenge = await dbContext.RegistrationChallenges.SingleOrDefaultAsync(
            item => item.Id == challengeId, cancellationToken);
        if (challenge is null) throw InvalidRegistrationCode();

        var now = timeProvider.GetUtcNow();
        var code = CreateCode();
        if (!challenge.TryResend(Hash(code), now))
        {
            throw new BusinessException(new ApplicationError(
                "registration_resend_unavailable", "A new verification code cannot be sent yet.", ApplicationErrorType.Conflict));
        }

        await SendRegistrationCodeAsync(challenge, code, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return challenge;
    }

    public async Task<(RegistrationChallenge Challenge, DateTimeOffset VerifiedAtUtc)> VerifyRegistrationAsync(
        Guid challengeId,
        string? code,
        CancellationToken cancellationToken = default)
    {
        if (challengeId == Guid.Empty || !IsValidCode(code)) throw InvalidRegistrationCode();
        var challenge = await dbContext.RegistrationChallenges.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == challengeId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (challenge is null || !challenge.IsUsableAt(now)) throw InvalidRegistrationCode();

        if (!MatchesHash(challenge.CodeHash, Hash(code!)))
        {
            await dbContext.RegistrationChallenges
                .Where(item => item.Id == challenge.Id && item.ConsumedAtUtc == null && item.ExpiresAtUtc > now &&
                    item.FailedAttemptCount < IdentityModuleConstants.Challenges.MaximumFailedAttempts)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.FailedAttemptCount, item => item.FailedAttemptCount + 1), cancellationToken);
            throw InvalidRegistrationCode();
        }

        return (challenge, now);
    }

    // The caller owns the transaction that creates the account or changes its password.
    public async Task ConsumeRegistrationAsync(
        RegistrationChallenge challenge,
        string code,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var codeHash = Hash(code);
        var consumed = await dbContext.RegistrationChallenges
            .Where(item => item.Id == challenge.Id && item.ConsumedAtUtc == null && item.ExpiresAtUtc > now &&
                item.FailedAttemptCount < IdentityModuleConstants.Challenges.MaximumFailedAttempts && item.CodeHash == codeHash)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ConsumedAtUtc, now), cancellationToken);
        if (consumed != 1) throw InvalidRegistrationCode();
    }

    public async Task SendPasswordResetAsync(
        Guid userId,
        ContactIdentifier contact,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var code = CreateCode();
        var codeHash = Hash(code);
        var challenge = await dbContext.PasswordResetOtps.SingleOrDefaultAsync(
            item => item.UserId == userId, cancellationToken);
        if (challenge is null)
        {
            challenge = new PasswordResetOtp(userId, contact.Value, codeHash, now);
            dbContext.PasswordResetOtps.Add(challenge);
        }
        else if (!challenge.TryResend(codeHash, now))
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        try
        {
            await contactOtpSender.SendAsync(contact, code, cancellationToken);
        }
        catch (Exception exception) when (exception is not BusinessException and not OperationCanceledException)
        {
            logger.LogWarning(exception, "Unable to send password reset SMS for user {UserId}.", userId);
            dbContext.PasswordResetOtps.Remove(challenge);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new BusinessException(new ApplicationError(
                ErrorCode.SmsUnavailable, ErrorCode.SmsUnavailable.Message, ApplicationErrorType.Conflict));
        }
    }

    public async Task<(PasswordResetOtp Challenge, DateTimeOffset VerifiedAtUtc)> VerifyPasswordResetAsync(
        Guid userId,
        string phoneNumber,
        string? code,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidCode(code)) throw InvalidPasswordReset();
        var challenge = await dbContext.PasswordResetOtps.AsNoTracking().SingleOrDefaultAsync(
            item => item.UserId == userId && item.PhoneNumber == phoneNumber, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (challenge is null || !challenge.IsUsableAt(now) || !MatchesHash(challenge.CodeHash, Hash(code!)))
        {
            if (challenge is not null && challenge.IsUsableAt(now))
            {
                await dbContext.PasswordResetOtps
                    .Where(item => item.Id == challenge.Id && item.ConsumedAtUtc == null && item.ExpiresAtUtc > now &&
                        item.FailedAttemptCount < IdentityModuleConstants.Challenges.MaximumFailedAttempts)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.FailedAttemptCount, item => item.FailedAttemptCount + 1), cancellationToken);
            }
            throw InvalidPasswordReset();
        }

        return (challenge, now);
    }

    public async Task ConsumePasswordResetAsync(
        PasswordResetOtp challenge,
        string code,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var codeHash = Hash(code);
        var consumed = await dbContext.PasswordResetOtps
            .Where(item => item.Id == challenge.Id && item.ConsumedAtUtc == null && item.ExpiresAtUtc > now &&
                item.FailedAttemptCount < IdentityModuleConstants.Challenges.MaximumFailedAttempts && item.CodeHash == codeHash)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ConsumedAtUtc, now), cancellationToken);
        if (consumed != 1) throw InvalidPasswordReset();
    }

    private async Task SendRegistrationCodeAsync(RegistrationChallenge challenge, string code, CancellationToken cancellationToken)
    {
        try
        {
            await contactOtpSender.SendAsync(new ContactIdentifier(challenge.ContactKind, challenge.Contact), code, cancellationToken);
        }
        catch (Exception exception) when (exception is not BusinessException and not OperationCanceledException)
        {
            logger.LogWarning(exception, "Unable to deliver a registration verification code through {ContactKind}.", challenge.ContactKind);
            dbContext.RegistrationChallenges.Remove(challenge);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new BusinessException(new ApplicationError(
                challenge.ContactKind == ContactKind.Email ? "email_delivery_unavailable" : "sms_delivery_unavailable",
                "The verification code could not be delivered.", ApplicationErrorType.Conflict));
        }
    }

    internal static bool IsValidCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && code.Length == 6 && code.All(char.IsAsciiDigit);

    private static string CreateCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool MatchesHash(string expected, string supplied) =>
        CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(supplied));

    private static BusinessException InvalidRegistrationCode() =>
        new(new ApplicationError("invalid_registration_code", "The verification code is invalid or expired.", ApplicationErrorType.Unauthorized));

    private static BusinessException InvalidPasswordReset() =>
        new(new ApplicationError(ErrorCode.InvalidPasswordReset, ErrorCode.InvalidPasswordReset.Message, ApplicationErrorType.Validation));
}
