using Fookbase.Api.Modules.Admin.Services;
using Fookbase.Api.Modules.Identity.Abstractions;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Domain.Enums;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Identity.Config;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class AuthenticationService(
    UserManager<User> userManager,
    FookbaseDbContext dbContext,
    JwtTokenService tokenService,
    IEmailSender emailSender,
    OtpService otpService,
    EmailOptions emailOptions,
    ILogger<AuthenticationService> logger,
    AccountModerationService accountModerationService,
    TimeProvider timeProvider)
{
    public async Task<object> LoginAsync(
        LoginRequest request,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var contact = ContactIdentifier.Parse(request.Identifier!);
        var user = await FindByIdentifierAsync(contact, cancellationToken);

        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user) ||
            await accountModerationService.IsUnavailableAsync(user.Id, cancellationToken))
        {
            throw Failure(ErrorCode.InvalidCredentials);
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password!))
        {
            await userManager.AccessFailedAsync(user);
            throw Failure(ErrorCode.InvalidCredentials);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        return await CompleteLoginAsync(user, userAgent, cancellationToken);
    }

    public async Task<object> CompleteExternalLoginAsync(
        User user,
        string? userAgent,
        string? pendingExternalProvider = null,
        string? pendingExternalProviderKey = null,
        CancellationToken cancellationToken = default)
    {
        if (!user.IsActive || await userManager.IsLockedOutAsync(user) ||
            await accountModerationService.IsUnavailableAsync(user.Id, cancellationToken))
        {
            throw Failure(ErrorCode.InvalidExternalLogin);
        }

        return await CompleteLoginAsync(
            user, userAgent, cancellationToken, pendingExternalProvider, pendingExternalProviderKey);
    }

    public Task<AuthenticationResponse> IssueSessionAsync(
        User user,
        string? userAgent,
        CancellationToken cancellationToken = default) =>
        IssueNewTokenPairAsync(user, timeProvider.GetUtcNow(), userAgent, cancellationToken);

    public async Task<AuthenticationResponse> VerifyTwoFactorAsync(TwoFactorVerifyRequest request,
        string? userAgent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Challenge) || string.IsNullOrWhiteSpace(request.Code) ||
            !Guid.TryParseExact(request.Challenge, "N", out var challengeId))
            throw Failure(ErrorCode.InvalidTwoFactorChallenge);
        var now = timeProvider.GetUtcNow();
        var challenge = await dbContext.TwoFactorLoginChallenges.SingleOrDefaultAsync(item => item.Id == challengeId, cancellationToken);
        if (challenge is null || !challenge.IsUsableAt(now))
            throw Failure(ErrorCode.InvalidTwoFactorChallenge);
        var user = await userManager.FindByIdAsync(challenge.UserId.ToString());
        if (user is null || !user.IsActive || !user.TwoFactorEnabled ||
            await accountModerationService.IsUnavailableAsync(user.Id, cancellationToken))
            throw Failure(ErrorCode.InvalidTwoFactorChallenge);
        var recoveryCode = request.Code.Trim();
        var valid = await VerifyAuthenticatorCodeAsync(user, recoveryCode) ||
            (await userManager.RedeemTwoFactorRecoveryCodeAsync(user, recoveryCode)).Succeeded;
        if (!valid)
            throw Failure(ErrorCode.InvalidTwoFactorCode);

        if (challenge.PendingExternalProvider is not null && challenge.PendingExternalProviderKey is not null)
        {
            var addLogin = await userManager.AddLoginAsync(
                user,
                new UserLoginInfo(
                    challenge.PendingExternalProvider,
                    challenge.PendingExternalProviderKey,
                    challenge.PendingExternalProvider));
            if (!addLogin.Succeeded)
            {
                throw Failure(ErrorCode.InvalidTwoFactorChallenge);
            }
        }

        challenge.Consume(now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await IssueNewTokenPairAsync(user, now, userAgent, cancellationToken);
    }

    public async Task<AuthenticationResponse> RefreshAsync(
        RefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw Failure(ErrorCode.InvalidRefreshToken);
        }

        var tokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var currentToken = await dbContext.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (currentToken is null || !currentToken.IsActiveAt(now))
        {
            throw Failure(ErrorCode.InvalidRefreshToken);
        }

        var user = await userManager.FindByIdAsync(currentToken.UserId.ToString());
        if (user is null || !user.IsActive || await accountModerationService.IsUnavailableAsync(user.Id, cancellationToken))
        {
            throw Failure(ErrorCode.InvalidRefreshToken);
        }

        var session = await dbContext.AuthSessions.SingleOrDefaultAsync(
            item => item.Id == currentToken.SessionId,
            cancellationToken);
        if (session is null || session.UserId != user.Id || !session.IsActiveAt(now))
        {
            throw Failure(ErrorCode.InvalidRefreshToken);
        }

        var replacement = tokenService.CreateRefreshToken(user.Id, session.Id, now);
        var rotated = await RotateRefreshTokenAsync(
            currentToken.Id,
            replacement.RefreshToken,
            now,
            cancellationToken);

        if (!rotated)
        {
            throw Failure(ErrorCode.InvalidRefreshToken);
        }

        session.Touch(now);
        await dbContext.SaveChangesAsync(cancellationToken);

        var roles = await GetRolesAsync(user);
        return BuildResponse(user, roles, tokenService.CreateAccessToken(user, roles, now, session.Id), replacement);
    }

    public async Task LogoutAsync(
        Guid userId,
        LogoutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw Failure(ErrorCode.InvalidRefreshToken);
        }

        var now = timeProvider.GetUtcNow();
        var currentToken = await dbContext.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(
            token => token.TokenHash == tokenService.HashRefreshToken(request.RefreshToken) && token.UserId == userId,
            cancellationToken);
        if (currentToken is null)
        {
            throw Failure(ErrorCode.InvalidRefreshToken);
        }

        await RevokeSessionAsync(userId, currentToken.SessionId, now, cancellationToken);
    }

    public async Task<AuthenticatedUserResponse> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetActiveUserAsync(userId);

        return ToResponse(user, await GetRolesAsync(user));
    }

    public async Task<SecurityStateResponse> GetSecurityAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetActiveUserAsync(userId);
        var now = timeProvider.GetUtcNow();
        return new SecurityStateResponse(user.TwoFactorEnabled,
            await userManager.CountRecoveryCodesAsync(user),
            await dbContext.AuthSessions.CountAsync(session => session.UserId == userId && session.RevokedAtUtc == null && session.ExpiresAtUtc > now, cancellationToken));
    }

    public async Task<TwoFactorSetupResponse> SetupTwoFactorAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetActiveUserAsync(userId);
        await userManager.ResetAuthenticatorKeyAsync(user);
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
            throw Failure(ErrorCode.TwoFactorSetupFailed, ApplicationErrorType.Validation);
        var issuer = Uri.EscapeDataString("Fookbase");
        var label = Uri.EscapeDataString($"Fookbase:{user.Email}");
        return new TwoFactorSetupResponse(key,
            $"otpauth://totp/{label}?secret={Uri.EscapeDataString(key)}&issuer={issuer}&digits=6");
    }

    public async Task<TwoFactorRecoveryCodesResponse> EnableTwoFactorAsync(Guid userId, string? code,
        CancellationToken cancellationToken = default)
    {
        var user = await GetActiveUserAsync(userId);
        if (!await VerifyAuthenticatorCodeAsync(user, code))
            throw Failure(ErrorCode.InvalidTwoFactorCode, ApplicationErrorType.Validation);
        var enable = await userManager.SetTwoFactorEnabledAsync(user, true);
        if (!enable.Succeeded)
            throw Failure(ErrorCode.TwoFactorEnableFailed, ApplicationErrorType.Validation);
        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        return new TwoFactorRecoveryCodesResponse(codes?.ToArray() ?? []);
    }

    public async Task<TwoFactorRecoveryCodesResponse> RegenerateRecoveryCodesAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive || !user.TwoFactorEnabled)
            throw Failure(ErrorCode.TwoFactorNotEnabled, ApplicationErrorType.Validation);
        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        return new TwoFactorRecoveryCodesResponse(codes?.ToArray() ?? []);
    }

    public async Task DisableTwoFactorAsync(Guid userId, string? currentPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await GetActiveUserAsync(userId);
        if (string.IsNullOrWhiteSpace(currentPassword) || !await userManager.CheckPasswordAsync(user, currentPassword))
            throw Failure(ErrorCode.InvalidCredentials, ApplicationErrorType.Validation);
        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        await RevokeOtherSessionsAsync(userId, null, timeProvider.GetUtcNow(), cancellationToken);
    }

    private async Task<bool> VerifyAuthenticatorCodeAsync(User user, string? code) =>
        !string.IsNullOrWhiteSpace(code) && await userManager.VerifyTwoFactorTokenAsync(user,
            TokenOptions.DefaultAuthenticatorProvider, code.Replace(" ", string.Empty).Replace("-", string.Empty));

    public async Task SendEmailVerificationAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetActiveUserAsync(userId);

        if (user.EmailConfirmed)
        {
            return;
        }

        if (!emailSender.IsEnabled)
        {
            throw Failure(ErrorCode.EmailUnavailable, ApplicationErrorType.Conflict);
        }

        try
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var verificationUrl = BuildFrontendUrl("verify", user.Email!, token);
            await emailSender.SendAsync(
                user.Email!,
                "Verify your Fookbase email",
                $"<p>Thanks for joining Fookbase.</p><p><a href=\"{verificationUrl}\">Verify email address</a></p>",
                cancellationToken);
        }
        catch (Exception exception) when (exception is not BusinessException and not OperationCanceledException)
        {
            logger.LogWarning(exception, "Unable to resend email verification for user {UserId}.", user.Id);
            throw Failure(ErrorCode.EmailUnavailable, ApplicationErrorType.Conflict);
        }
    }

    public async Task VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token))
        {
            throw Failure(ErrorCode.InvalidVerificationLink, ApplicationErrorType.Validation);
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
        {
            throw Failure(ErrorCode.InvalidVerificationLink, ApplicationErrorType.Validation);
        }

        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
            throw Failure(ErrorCode.InvalidVerificationLink, ApplicationErrorType.Validation);
    }

    public async Task RequestPasswordResetAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var contact = ContactIdentifier.Parse(request.Identifier!);

        if (contact.Kind == ContactKind.Phone)
        {
            var phoneUser = await FindByIdentifierAsync(contact, cancellationToken);
            if (phoneUser is { IsActive: true })
            {
                await otpService.SendPasswordResetAsync(phoneUser.Id, contact, cancellationToken);
            }
            return;
        }

        if (!emailSender.IsEnabled)
        {
            throw Failure(ErrorCode.EmailUnavailable, ApplicationErrorType.Conflict);
        }

        var user = await FindByIdentifierAsync(contact, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return;
        }

        try
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var resetUrl = BuildFrontendUrl("reset", user.Email!, token);
            await emailSender.SendAsync(
                user.Email!,
                "Reset your Fookbase password",
                $"<p>We received a request to reset your Fookbase password.</p><p><a href=\"{resetUrl}\">Reset password</a></p><p>If you did not request this, you can ignore this email.</p>",
                cancellationToken);
        }
        catch (Exception exception) when (exception is not BusinessException and not OperationCanceledException)
        {
            logger.LogWarning(exception, "Unable to send password reset email for user {UserId}.", user.Id);
            throw Failure(ErrorCode.EmailUnavailable, ApplicationErrorType.Conflict);
        }
    }

    public async Task ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var contact = ContactIdentifier.Parse(request.Identifier!);
        if (contact.Kind == ContactKind.Phone)
        {
            await ResetPhonePasswordAsync(contact, request, cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Identifier) || string.IsNullOrWhiteSpace(request.Token) ||
            string.IsNullOrWhiteSpace(request.Password) || request.Password != request.ConfirmPassword)
        {
            throw Failure(ErrorCode.InvalidPasswordReset, ApplicationErrorType.Validation);
        }

        var user = await FindByIdentifierAsync(contact, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw Failure(ErrorCode.InvalidPasswordReset, ApplicationErrorType.Validation);
        }

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.Password);
        if (!result.Succeeded)
        {
            throw Failure(ErrorCode.ValidationFailed, ApplicationErrorType.Validation, ToErrors(result));
        }

        await RevokeAllRefreshTokensAsync(user.Id, timeProvider.GetUtcNow(), cancellationToken);
    }

    private Task<User?> FindByIdentifierAsync(
        ContactIdentifier contact,
        CancellationToken cancellationToken = default) =>
        contact.Kind == ContactKind.Email
            ? userManager.FindByEmailAsync(contact.Value)
            : dbContext.Users.SingleOrDefaultAsync(user => user.PhoneNumber == contact.Value, cancellationToken);

    private async Task ResetPhonePasswordAsync(
        ContactIdentifier contact,
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!OtpService.IsValidCode(request.Code) ||
            string.IsNullOrWhiteSpace(request.Password) || request.Password != request.ConfirmPassword)
        {
            throw Failure(ErrorCode.InvalidPasswordReset, ApplicationErrorType.Validation);
        }

        var user = await FindByIdentifierAsync(contact, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw Failure(ErrorCode.InvalidPasswordReset, ApplicationErrorType.Validation);
        }

        var (challenge, now) = await otpService.VerifyPasswordResetAsync(user.Id, contact.Value, request.Code, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await otpService.ConsumePasswordResetAsync(challenge, request.Code!, now, cancellationToken);

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await userManager.ResetPasswordAsync(user, token, request.Password);
        if (!reset.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw Failure(ErrorCode.ValidationFailed, ApplicationErrorType.Validation, ToErrors(reset));
        }

        await RevokeAllRefreshTokensAsync(user.Id, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AuthenticationResponse> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword) ||
            request.NewPassword != request.ConfirmPassword)
        {
            throw Failure(ErrorCode.ValidationFailed, ApplicationErrorType.Validation,
                new Dictionary<string, string[]>
                {
                    ["password"] = ["Current password, matching new password, and confirmation are required."]
                });
        }

        var user = await GetActiveUserAsync(userId);

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            throw Failure(ErrorCode.ValidationFailed, ApplicationErrorType.Validation, ToErrors(result));
        }

        var now = timeProvider.GetUtcNow();
        await RevokeOtherSessionsAsync(user.Id, null, now, cancellationToken);
        return await IssueNewTokenPairAsync(user, now, null, cancellationToken);
    }

    private async Task<User> GetActiveUserAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is { IsActive: true } ? user : throw Failure(ErrorCode.InvalidAccessToken);
    }

    private async Task<object> CompleteLoginAsync(
        User user,
        string? userAgent,
        CancellationToken cancellationToken,
        string? pendingExternalProvider = null,
        string? pendingExternalProviderKey = null)
    {
        var now = timeProvider.GetUtcNow();
        if (!user.TwoFactorEnabled)
        {
            return await IssueNewTokenPairAsync(user, now, userAgent, cancellationToken);
        }

        var challenge = new TwoFactorLoginChallenge(user.Id, now, pendingExternalProvider, pendingExternalProviderKey);
        dbContext.TwoFactorLoginChallenges.Add(challenge);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new TwoFactorChallengeResponse(true, challenge.Id.ToString("N"), challenge.ExpiresAtUtc);
    }

    private async Task<AuthenticationResponse> IssueNewTokenPairAsync(
        User user,
        DateTimeOffset now,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var roles = await GetRolesAsync(user);
        var session = new AuthSession(user.Id, userAgent, now, now.AddDays(30));
        var accessToken = tokenService.CreateAccessToken(user, roles, now, session.Id);
        var refreshToken = tokenService.CreateRefreshToken(user.Id, session.Id, now);

        dbContext.AuthSessions.Add(session);
        dbContext.RefreshTokens.Add(refreshToken.RefreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return BuildResponse(user, roles, accessToken, refreshToken);
    }

    private static AuthenticationResponse BuildResponse(
        User user,
        IReadOnlyList<string> roles,
        AccessTokenResult accessToken,
        RefreshTokenResult refreshToken) =>
        new(
            ToResponse(user, roles),
            accessToken.Token,
            accessToken.ExpiresAt,
            refreshToken.RawToken,
            refreshToken.RefreshToken.ExpiresAt);

    private static AuthenticatedUserResponse ToResponse(User user, IReadOnlyList<string> roles) =>
        new(
            user.Id,
            user.Email,
            user.PhoneNumber,
            user.UserName!,
            user.EmailConfirmed,
            user.PhoneNumberConfirmed,
            roles);

    private async Task<IReadOnlyList<string>> GetRolesAsync(User user) =>
        (await userManager.GetRolesAsync(user))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private async Task<bool> RotateRefreshTokenAsync(
        Guid currentTokenId,
        RefreshToken replacement,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.RefreshTokens.Add(replacement);
        await dbContext.SaveChangesAsync(cancellationToken);

        var updatedRows = await dbContext.RefreshTokens
            .Where(token => token.Id == currentTokenId && token.RevokedAt == null && token.ExpiresAt > revokedAt)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAt, revokedAt)
                .SetProperty(token => token.ReplacedByTokenId, replacement.Id), cancellationToken);
        if (updatedRows != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.Entry(replacement).State = EntityState.Detached;
            return false;
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private Task RevokeAllRefreshTokensAsync(
        Guid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken) =>
        dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null && token.ExpiresAt > revokedAt)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAt, revokedAt),
                cancellationToken);

    public async Task<IReadOnlyList<AuthSessionResponse>> GetSessionsAsync(
        Guid userId, Guid? currentSessionId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        return await dbContext.AuthSessions.AsNoTracking()
            .Where(session => session.UserId == userId && session.RevokedAtUtc == null && session.ExpiresAtUtc > now)
            .OrderByDescending(session => session.LastSeenAtUtc)
            .Select(session => new AuthSessionResponse(session.Id, session.UserAgent, session.CreatedAtUtc,
                session.LastSeenAtUtc, session.ExpiresAtUtc, session.Id == currentSessionId))
            .ToListAsync(cancellationToken);
    }

    public async Task RevokeSessionAsync(
        Guid userId, Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var session = await dbContext.AuthSessions.SingleOrDefaultAsync(
            item => item.Id == sessionId && item.UserId == userId, cancellationToken);
        if (session is null)
            throw Failure(ErrorCode.SessionNotFound, ApplicationErrorType.NotFound);
        session.Revoke(now);
        await dbContext.RefreshTokens.Where(token => token.SessionId == sessionId && token.RevokedAt == null && token.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeOtherSessionsAsync(Guid userId, Guid? currentSessionId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sessionIds = await dbContext.AuthSessions
            .Where(session => session.UserId == userId && session.RevokedAtUtc == null && session.ExpiresAtUtc > now &&
                              (currentSessionId == null || session.Id != currentSessionId))
            .Select(session => session.Id)
            .ToListAsync(cancellationToken);
        if (sessionIds.Count == 0) return;
        await dbContext.AuthSessions.Where(session => sessionIds.Contains(session.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAtUtc, now), cancellationToken);
        await dbContext.RefreshTokens.Where(token => sessionIds.Contains(token.SessionId) && token.RevokedAt == null && token.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);
    }

    private string BuildFrontendUrl(string mode, string email, string token) =>
        $"{emailOptions.FrontendBaseUrl.TrimEnd('/')}/login?mode={mode}&email={WebUtility.UrlEncode(email)}&token={WebUtility.UrlEncode(token)}";

    private static BusinessException Failure(
        ErrorCode errorCode,
        ApplicationErrorType type = ApplicationErrorType.Unauthorized,
        IReadOnlyDictionary<string, string[]>? details = null) =>
        new(new ApplicationError(errorCode.Code, errorCode.Message, type, details));

    private static IReadOnlyDictionary<string, string[]> ToErrors(IdentityResult result) =>
        result.Errors
            .GroupBy(error => error.Code, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray(),
                StringComparer.Ordinal);
}
