using System.Security.Cryptography;
using System.Text;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Domain.Enums;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Users.Services;
using Fookbase.Api.Persistence;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed record GoogleCompletionResult(string Code, bool RequiresPassword, string? Email);

public sealed class GoogleAuthenticationService(
    UserManager<User> userManager,
    FookbaseDbContext dbContext,
    UserProfileService userProfileService,
    UserPrivacySettingsService privacySettingsService,
    AuthenticationService authenticationService,
    AccountModerationService accountModerationService,
    TimeProvider timeProvider)
{
    public async Task<GoogleCompletionResult> CreateCompletionAsync(
        string client,
        string providerKey,
        string email,
        bool emailVerified,
        CancellationToken cancellationToken = default)
    {
        if (!emailVerified || string.IsNullOrWhiteSpace(providerKey) || string.IsNullOrWhiteSpace(email))
        {
            throw Failure("invalid_google_identity", "The Google identity is invalid.");
        }

        var normalizedEmail = email.Trim();
        var linkedUser = await userManager.FindByLoginAsync(IdentityModuleConstants.ExternalLogin.GoogleScheme, providerKey);
        if (linkedUser is not null)
        {
            return await CreateCompletionAsync(
                ExternalLoginTicketPurpose.IssueSession,
                client,
                providerKey,
                normalizedEmail,
                linkedUser.Id,
                cancellationToken);
        }

        var existingUser = await userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser is not null)
        {
            return await CreateCompletionAsync(
                ExternalLoginTicketPurpose.LinkExisting,
                client,
                providerKey,
                normalizedEmail,
                existingUser.Id,
                cancellationToken);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow();
            var username = await GenerateUsernameAsync(normalizedEmail, cancellationToken);
            var user = new User(Guid.NewGuid(), normalizedEmail, username, now)
            {
                EmailConfirmed = true
            };
            var createUser = await userManager.CreateAsync(user);
            if (!createUser.Succeeded)
            {
                throw Failure("google_account_creation_failed", "The Google account could not be created.");
            }

            await userProfileService.EnsureCreatedAsync(user.Id, username, cancellationToken);
            await privacySettingsService.EnsureCreatedAsync(user.Id, cancellationToken);
            var addLogin = await userManager.AddLoginAsync(user, new UserLoginInfo(
                IdentityModuleConstants.ExternalLogin.GoogleScheme,
                providerKey,
                IdentityModuleConstants.ExternalLogin.GoogleScheme));
            if (!addLogin.Succeeded)
            {
                throw Failure("google_account_creation_failed", "The Google account could not be created.");
            }

            var completion = await CreateCompletionAsync(
                ExternalLoginTicketPurpose.IssueSession,
                client,
                providerKey,
                normalizedEmail,
                user.Id,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return completion;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<object> ExchangeAsync(
        string code,
        string client,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var completion = await FindUsableCompletionAsync(code, client, ExternalLoginTicketPurpose.IssueSession, cancellationToken);
        if (completion is null)
        {
            throw Failure("invalid_google_completion", "The Google sign-in could not be completed.");
        }

        if (!await TryConsumeCompletionAsync(completion, cancellationToken))
        {
            throw Failure("invalid_google_completion", "The Google sign-in could not be completed.");
        }

        var user = await userManager.FindByIdAsync(completion.UserId.ToString());
        return user is null
            ? throw Failure("invalid_google_completion", "The Google sign-in could not be completed.")
            : await authenticationService.CompleteExternalLoginAsync(
                user,
                userAgent,
                cancellationToken: cancellationToken);
    }

    public async Task<object> LinkExistingAsync(
        string code,
        string client,
        string password,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var completion = await FindUsableCompletionAsync(
            code,
            client,
            ExternalLoginTicketPurpose.LinkExisting,
            cancellationToken);
        if (completion is null)
        {
            throw Failure("invalid_google_completion", "The Google sign-in could not be completed.");
        }

        var user = await userManager.FindByIdAsync(completion.UserId.ToString());
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user) ||
            await accountModerationService.IsUnavailableAsync(user.Id, cancellationToken))
        {
            throw Failure("invalid_google_completion", "The Google sign-in could not be completed.");
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            throw Failure(ErrorCode.InvalidCredentials, "The email or password is invalid.");
        }

        await userManager.ResetAccessFailedCountAsync(user);
        if (!await TryConsumeCompletionAsync(completion, cancellationToken))
        {
            throw Failure("invalid_google_completion", "The Google sign-in could not be completed.");
        }

        if (user.TwoFactorEnabled)
        {
            return await authenticationService.CompleteExternalLoginAsync(
                user,
                userAgent,
                IdentityModuleConstants.ExternalLogin.GoogleScheme,
                completion.ProviderKey,
                cancellationToken);
        }

        var addLogin = await userManager.AddLoginAsync(user, new UserLoginInfo(
            IdentityModuleConstants.ExternalLogin.GoogleScheme,
            completion.ProviderKey,
            IdentityModuleConstants.ExternalLogin.GoogleScheme));
        if (!addLogin.Succeeded)
        {
            throw Failure("google_link_failed", "The Google account could not be linked.");
        }

        return await authenticationService.CompleteExternalLoginAsync(
            user,
            userAgent,
            cancellationToken: cancellationToken);
    }

    private async Task<GoogleCompletionResult> CreateCompletionAsync(
        ExternalLoginTicketPurpose purpose,
        string client,
        string providerKey,
        string email,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var rawCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var codeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawCode)));
        var completion = new ExternalLoginTicket(
            codeHash,
            purpose,
            client,
            IdentityModuleConstants.ExternalLogin.GoogleScheme,
            providerKey,
            email,
            userId,
            timeProvider.GetUtcNow());
        dbContext.ExternalLoginTickets.Add(completion);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new GoogleCompletionResult(
            rawCode,
            purpose == ExternalLoginTicketPurpose.LinkExisting,
            purpose == ExternalLoginTicketPurpose.LinkExisting ? email : null);
    }

    private async Task<ExternalLoginTicket?> FindUsableCompletionAsync(
        string code,
        string client,
        ExternalLoginTicketPurpose purpose,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(client))
        {
            return null;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
        var completion = await dbContext.ExternalLoginTickets.AsNoTracking().SingleOrDefaultAsync(
            item => item.CodeHash == hash && item.Client == client && item.Purpose == purpose,
            cancellationToken);
        if (completion is null || !completion.IsUsableAt(timeProvider.GetUtcNow()))
        {
            return null;
        }

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(completion.CodeHash),
            Convert.FromHexString(hash))
            ? completion
            : null;
    }

    private async Task<bool> TryConsumeCompletionAsync(
        ExternalLoginTicket completion,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return await dbContext.ExternalLoginTickets
            .Where(item => item.Id == completion.Id && item.ConsumedAtUtc == null && item.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.ConsumedAtUtc, now),
                cancellationToken) == 1;
    }

    private async Task<string> GenerateUsernameAsync(string email, CancellationToken cancellationToken)
    {
        var localPart = email.Split('@')[0].ToLowerInvariant();
        var sanitized = new string(localPart.Where(character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-').ToArray())
            .Trim('.', '_', '-');
        var baseUsername = sanitized.Length < 3 ? "google-user" : sanitized[..Math.Min(sanitized.Length, 28)];

        for (var suffix = 1; ; suffix++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = suffix == 1 ? baseUsername : $"{baseUsername[..Math.Min(baseUsername.Length, 32 - suffix.ToString().Length - 1)]}-{suffix}";
            if (await userManager.FindByNameAsync(candidate) is null)
            {
                return candidate;
            }
        }
    }

    private static BusinessException Failure(string code, string message) =>
        new BusinessException(new ApplicationError(code, message, ApplicationErrorType.Unauthorized));
}
