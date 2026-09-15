using System.Security.Cryptography;
using System.Text;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Users.Services;
using Fookbase.Api.Persistence;
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
    private const string Provider = "Google";

    public async Task<ApplicationResult<GoogleCompletionResult>> CreateCompletionAsync(
        string client,
        string providerKey,
        string email,
        bool emailVerified,
        CancellationToken cancellationToken = default)
    {
        if (!emailVerified || string.IsNullOrWhiteSpace(providerKey) || string.IsNullOrWhiteSpace(email))
        {
            return Failure<GoogleCompletionResult>("invalid_google_identity", "The Google identity is invalid.");
        }

        var normalizedEmail = email.Trim();
        var linkedUser = await userManager.FindByLoginAsync(Provider, providerKey);
        if (linkedUser is not null)
        {
            return await CreateCompletionAsync(
                ExternalLoginCompletionPurpose.IssueSession,
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
                ExternalLoginCompletionPurpose.LinkExisting,
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
                await transaction.RollbackAsync(CancellationToken.None);
                return Failure<GoogleCompletionResult>("google_account_creation_failed", "The Google account could not be created.");
            }

            await userProfileService.EnsureCreatedAsync(user.Id, username, cancellationToken);
            await privacySettingsService.EnsureCreatedAsync(user.Id, cancellationToken);
            var addLogin = await userManager.AddLoginAsync(user, new UserLoginInfo(Provider, providerKey, Provider));
            if (!addLogin.Succeeded)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Failure<GoogleCompletionResult>("google_account_creation_failed", "The Google account could not be created.");
            }

            var completion = await CreateCompletionAsync(
                ExternalLoginCompletionPurpose.IssueSession,
                client,
                providerKey,
                normalizedEmail,
                user.Id,
                cancellationToken);
            if (!completion.Succeeded)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return completion;
            }

            await transaction.CommitAsync(cancellationToken);
            return completion;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<ApplicationResult<object>> ExchangeAsync(
        string code,
        string client,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var completion = await FindUsableCompletionAsync(code, client, ExternalLoginCompletionPurpose.IssueSession, cancellationToken);
        if (completion is null || completion.UserId is not { } userId)
        {
            return Failure<object>("invalid_google_completion", "The Google sign-in could not be completed.");
        }

        if (!await TryConsumeCompletionAsync(completion, cancellationToken))
        {
            return Failure<object>("invalid_google_completion", "The Google sign-in could not be completed.");
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null
            ? Failure<object>("invalid_google_completion", "The Google sign-in could not be completed.")
            : await authenticationService.CompleteExternalLoginAsync(
                user,
                userAgent,
                cancellationToken: cancellationToken);
    }

    public async Task<ApplicationResult<object>> LinkExistingAsync(
        string code,
        string client,
        string password,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var completion = await FindUsableCompletionAsync(
            code,
            client,
            ExternalLoginCompletionPurpose.LinkExisting,
            cancellationToken);
        if (completion is null || completion.UserId is not { } userId)
        {
            return Failure<object>("invalid_google_completion", "The Google sign-in could not be completed.");
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user) ||
            await accountModerationService.IsUnavailableAsync(user.Id, cancellationToken))
        {
            return Failure<object>("invalid_google_completion", "The Google sign-in could not be completed.");
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            return Failure<object>("invalid_credentials", "The email or password is invalid.");
        }

        await userManager.ResetAccessFailedCountAsync(user);
        if (!await TryConsumeCompletionAsync(completion, cancellationToken))
        {
            return Failure<object>("invalid_google_completion", "The Google sign-in could not be completed.");
        }

        if (user.TwoFactorEnabled)
        {
            return await authenticationService.CompleteExternalLoginAsync(
                user,
                userAgent,
                Provider,
                completion.ProviderKey,
                cancellationToken);
        }

        var addLogin = await userManager.AddLoginAsync(user, new UserLoginInfo(Provider, completion.ProviderKey, Provider));
        if (!addLogin.Succeeded)
        {
            return Failure<object>("google_link_failed", "The Google account could not be linked.");
        }

        return await authenticationService.CompleteExternalLoginAsync(
            user,
            userAgent,
            cancellationToken: cancellationToken);
    }

    private async Task<ApplicationResult<GoogleCompletionResult>> CreateCompletionAsync(
        ExternalLoginCompletionPurpose purpose,
        string client,
        string providerKey,
        string email,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var rawCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var codeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawCode)));
        var completion = ExternalLoginCompletion.Create(
            codeHash,
            purpose,
            client,
            Provider,
            providerKey,
            email,
            userId,
            timeProvider.GetUtcNow());
        dbContext.ExternalLoginCompletions.Add(completion);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult<GoogleCompletionResult>.Success(new GoogleCompletionResult(
            rawCode,
            purpose == ExternalLoginCompletionPurpose.LinkExisting,
            purpose == ExternalLoginCompletionPurpose.LinkExisting ? email : null));
    }

    private async Task<ExternalLoginCompletion?> FindUsableCompletionAsync(
        string code,
        string client,
        ExternalLoginCompletionPurpose purpose,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(client))
        {
            return null;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
        var completion = await dbContext.ExternalLoginCompletions.AsNoTracking().SingleOrDefaultAsync(
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
        ExternalLoginCompletion completion,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return await dbContext.ExternalLoginCompletions
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

    private static ApplicationResult<T> Failure<T>(string code, string message) =>
        ApplicationResult<T>.Failure(new ApplicationError(code, message, ApplicationErrorType.Unauthorized));
}
