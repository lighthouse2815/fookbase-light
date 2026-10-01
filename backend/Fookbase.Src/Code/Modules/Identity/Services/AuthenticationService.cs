using Fookbase.Api.Shared.Common;
using Fookbase.Api.Persistence;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Identity.Config;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text;
using System.Security.Cryptography;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class AuthenticationService(
    UserManager<User> userManager,
    FookbaseDbContext dbContext,
    JwtTokenService tokenService,
    RoleManager<IdentityRole<Guid>> roleManager,
    IEmailSender emailSender,
    IContactOtpSender contactOtpSender,
    EmailOptions emailOptions,
    AdminOptions adminOptions,
    ILogger<AuthenticationService> logger,
    AccountModerationService accountModerationService,
    TimeProvider timeProvider)
{
    public async Task<AuthenticationResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = AuthenticationValidation.Validate(request);
        if (validationErrors.Count > 0)
        {
            throw ValidationFailure(validationErrors);
        }

        var email = request.Email!.Trim();
        var userName = request.Username!.Trim();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw ConflictFailure(
                "duplicate_email",
                "An account with this email already exists.");
        }

        if (await userManager.FindByNameAsync(userName) is not null)
        {
            throw ConflictFailure(
                "duplicate_username",
                "An account with this username already exists.");
        }

        var now = timeProvider.GetUtcNow();
        var user = new User(Guid.NewGuid(), email, userName, now);
        var session = new AuthSession(user.Id, null, now, now.AddDays(30));
        var refreshToken = tokenService.CreateRefreshToken(user.Id, session.Id, now);
        var creationResult = await CreateUserAsync(
            user,
            request.Password!,
            refreshToken.RefreshToken,
            session,
            cancellationToken);

        if (!creationResult.Succeeded)
        {
            var errors = ToErrors(creationResult);
            if (errors.ContainsKey("DuplicateEmail"))
            {
                throw ConflictFailure(
                    "duplicate_email",
                    "An account with this email already exists.");
            }

            if (errors.ContainsKey("DuplicateUserName"))
            {
                throw ConflictFailure(
                    "duplicate_username",
                    "An account with this username already exists.");
            }

            throw ValidationFailure(errors);
        }

        if (emailSender.IsEnabled)
        {
            try
            {
                await SendEmailVerificationForUserAsync(user, cancellationToken);
            }
            catch (Exception exception) when (exception is not BusinessException and not OperationCanceledException)
            {
                logger.LogWarning(exception, "Unable to send email verification for user {UserId}.", user.Id);
            }
        }

        var roles = await GetRolesAsync(user);
        return BuildResponse(user, roles, tokenService.CreateAccessToken(user, roles, now, session.Id), refreshToken);
    }

    public async Task<object> LoginAsync(
        LoginRequest request,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = AuthenticationValidation.Validate(request);
        if (validationErrors.Count > 0)
        {
            throw ValidationFailure(validationErrors);
        }

        var user = await FindByIdentifierAsync(request.EffectiveIdentifier, cancellationToken);
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user) ||
            await accountModerationService.IsUnavailableAsync(user.Id, cancellationToken))
        {
            throw UnauthorizedFailure(
                ErrorCode.InvalidCredentials,
                "The email, phone number, or password is invalid.");
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password!))
        {
            await userManager.AccessFailedAsync(user);
            throw UnauthorizedFailure(ErrorCode.InvalidCredentials, "The email, phone number, or password is invalid.");
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var now = timeProvider.GetUtcNow();
        if (user.TwoFactorEnabled)
        {
            var challenge = TwoFactorLoginChallenge.Create(user.Id, now);
            dbContext.TwoFactorLoginChallenges.Add(challenge);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new TwoFactorChallengeResponse(true, challenge.Id.ToString("N"), challenge.ExpiresAtUtc);
        }

        return await IssueNewTokenPairAsync(user, now, userAgent, cancellationToken);
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
            throw UnauthorizedFailure(
                "invalid_external_login",
                "The external login is unavailable.");
        }

        var now = timeProvider.GetUtcNow();
        if (user.TwoFactorEnabled)
        {
            var challenge = TwoFactorLoginChallenge.Create(
                user.Id,
                now,
                pendingExternalProvider,
                pendingExternalProviderKey);
            dbContext.TwoFactorLoginChallenges.Add(challenge);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new TwoFactorChallengeResponse(true, challenge.Id.ToString("N"), challenge.ExpiresAtUtc);
        }

        return await IssueNewTokenPairAsync(user, now, userAgent, cancellationToken);
    }

    public async Task<User?> FindByIdentifierAsync(
        string? identifier,
        CancellationToken cancellationToken = default)
    {
        if (!ContactIdentifier.TryParse(identifier, out var contact)) return null;
        return contact.Kind == ContactKind.Email
            ? await userManager.FindByEmailAsync(contact.Value)
            : await dbContext.Users.SingleOrDefaultAsync(user => user.PhoneNumber == contact.Value, cancellationToken);
    }

    public Task<AuthenticationResponse> IssueSessionAsync(
        User user,
        string? userAgent,
        CancellationToken cancellationToken = default) =>
        IssueNewTokenPairAsync(user, timeProvider.GetUtcNow(), userAgent, cancellationToken);

    public async Task<AuthenticationResponse> VerifyTwoFactorAsync(TwoFactorVerifyRequest request,
        string? userAgent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Challenge) || string.IsNullOrWhiteSpace(request.Code))
            throw UnauthorizedFailure("invalid_two_factor_challenge", "The two-factor challenge is invalid or expired.");
        if (!Guid.TryParseExact(request.Challenge, "N", out var challengeId))
            throw UnauthorizedFailure("invalid_two_factor_challenge", "The two-factor challenge is invalid or expired.");
        var now = timeProvider.GetUtcNow();
        var challenge = await dbContext.TwoFactorLoginChallenges.SingleOrDefaultAsync(item => item.Id == challengeId, cancellationToken);
        if (challenge is null || !challenge.IsUsableAt(now)) throw UnauthorizedFailure("invalid_two_factor_challenge", "The two-factor challenge is invalid or expired.");
        var user = await userManager.FindByIdAsync(challenge.UserId.ToString());
        if (user is null || !user.IsActive || !user.TwoFactorEnabled ||
            await accountModerationService.IsUnavailableAsync(user.Id, cancellationToken))
            throw UnauthorizedFailure("invalid_two_factor_challenge", "The two-factor challenge is invalid or expired.");
        var recoveryCode = request.Code.Trim();
        var valid = await VerifyAuthenticatorCodeAsync(user, recoveryCode) ||
            (await userManager.RedeemTwoFactorRecoveryCodeAsync(user, recoveryCode)).Succeeded;
        if (!valid) throw UnauthorizedFailure("invalid_two_factor_code", "The two-factor code is invalid.");

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
                throw UnauthorizedFailure(
                    "invalid_two_factor_challenge",
                    "The two-factor challenge is invalid or expired.");
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
            throw UnauthorizedFailure(
                "invalid_refresh_token",
                "The refresh token is invalid or expired.");
        }

        var tokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var currentToken = await dbContext.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (currentToken is null || !currentToken.IsActiveAt(now))
        {
            throw UnauthorizedFailure(
                "invalid_refresh_token",
                "The refresh token is invalid or expired.");
        }

        var user = await userManager.FindByIdAsync(currentToken.UserId.ToString());
        if (user is null || !user.IsActive || await accountModerationService.IsUnavailableAsync(user.Id, cancellationToken))
        {
            throw UnauthorizedFailure(
                "invalid_refresh_token",
                "The refresh token is invalid or expired.");
        }

        var session = await dbContext.AuthSessions.SingleOrDefaultAsync(
            item => item.Id == currentToken.SessionId,
            cancellationToken);
        if (session is null || session.UserId != user.Id || !session.IsActiveAt(now))
        {
            throw UnauthorizedFailure("invalid_refresh_token", "The refresh token is invalid or expired.");
        }

        var replacement = tokenService.CreateRefreshToken(user.Id, session.Id, now);
        var rotated = await RotateRefreshTokenAsync(
            currentToken.Id,
            replacement.RefreshToken,
            now,
            cancellationToken);

        if (!rotated)
        {
            throw UnauthorizedFailure(
                "invalid_refresh_token",
                "The refresh token is invalid or expired.");
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
            throw new BusinessException(InvalidRefreshToken());
        }

        var now = timeProvider.GetUtcNow();
        var currentToken = await dbContext.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(
            token => token.TokenHash == tokenService.HashRefreshToken(request.RefreshToken) && token.UserId == userId,
            cancellationToken);
        if (currentToken is null)
        {
            throw new BusinessException(InvalidRefreshToken());
        }

        await RevokeSessionAsync(userId, currentToken.SessionId, now, cancellationToken);
    }

    public async Task<AuthenticatedUserResponse> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            throw UnauthorizedFailure(
                ErrorCode.InvalidAccessToken,
                "The access token is invalid.");
        }

        return ToResponse(user, await GetRolesAsync(user));
    }

    public async Task<SecurityStateResponse> GetSecurityAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive) throw UnauthorizedFailure(ErrorCode.InvalidAccessToken, "The access token is invalid.");
        var now = timeProvider.GetUtcNow();
        return new SecurityStateResponse(user.TwoFactorEnabled,
            await userManager.CountRecoveryCodesAsync(user),
            await dbContext.AuthSessions.CountAsync(session => session.UserId == userId && session.RevokedAtUtc == null && session.ExpiresAtUtc > now, cancellationToken));
    }

    public async Task<TwoFactorSetupResponse> SetupTwoFactorAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive) throw UnauthorizedFailure(ErrorCode.InvalidAccessToken, "The access token is invalid.");
        await userManager.ResetAuthenticatorKeyAsync(user);
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key)) throw new BusinessException(new ApplicationError("two_factor_setup_failed", "Two-factor setup could not be initialized.", ApplicationErrorType.Validation));
        var issuer = Uri.EscapeDataString("Fookbase");
        var label = Uri.EscapeDataString($"Fookbase:{user.Email}");
        return new TwoFactorSetupResponse(key,
            $"otpauth://totp/{label}?secret={Uri.EscapeDataString(key)}&issuer={issuer}&digits=6");
    }

    public async Task<TwoFactorRecoveryCodesResponse> EnableTwoFactorAsync(Guid userId, string? code,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive) throw UnauthorizedFailure(ErrorCode.InvalidAccessToken, "The access token is invalid.");
        if (!await VerifyAuthenticatorCodeAsync(user, code)) throw new BusinessException(new ApplicationError("invalid_two_factor_code", "The authenticator code is invalid.", ApplicationErrorType.Validation));
        var enable = await userManager.SetTwoFactorEnabledAsync(user, true);
        if (!enable.Succeeded) throw new BusinessException(new ApplicationError("two_factor_enable_failed", "Two-factor authentication could not be enabled.", ApplicationErrorType.Validation));
        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        return new TwoFactorRecoveryCodesResponse(codes?.ToArray() ?? []);
    }

    public async Task<TwoFactorRecoveryCodesResponse> RegenerateRecoveryCodesAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive || !user.TwoFactorEnabled) throw new BusinessException(new ApplicationError("two_factor_not_enabled", "Two-factor authentication is not enabled.", ApplicationErrorType.Validation));
        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        return new TwoFactorRecoveryCodesResponse(codes?.ToArray() ?? []);
    }

    public async Task DisableTwoFactorAsync(Guid userId, string? currentPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive) throw new BusinessException(new ApplicationError(ErrorCode.InvalidAccessToken, "The access token is invalid.", ApplicationErrorType.Unauthorized));
        if (string.IsNullOrWhiteSpace(currentPassword) || !await userManager.CheckPasswordAsync(user, currentPassword)) throw new BusinessException(new ApplicationError(ErrorCode.InvalidCredentials, "The current password is invalid.", ApplicationErrorType.Validation));
        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        await RevokeOtherSessionsAsync(userId, null, timeProvider.GetUtcNow(), cancellationToken);
        return;
    }

    private async Task<bool> VerifyAuthenticatorCodeAsync(User user, string? code) =>
        !string.IsNullOrWhiteSpace(code) && await userManager.VerifyTwoFactorTokenAsync(user,
            TokenOptions.DefaultAuthenticatorProvider, code.Replace(" ", string.Empty).Replace("-", string.Empty));

    public async Task SendEmailVerificationAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            throw new BusinessException(new ApplicationError(
                ErrorCode.InvalidAccessToken, ErrorCode.InvalidAccessToken.Message, ApplicationErrorType.Unauthorized));
        }

        if (user.EmailConfirmed)
        {
            return;
        }

        if (!emailSender.IsEnabled)
        {
            throw new BusinessException(EmailUnavailable());
        }

        try
        {
            await SendEmailVerificationForUserAsync(user, cancellationToken);
            return;
        }
        catch (Exception exception) when (exception is not BusinessException and not OperationCanceledException)
        {
            logger.LogWarning(exception, "Unable to resend email verification for user {UserId}.", user.Id);
            throw new BusinessException(EmailUnavailable());
        }
    }

    public async Task VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token))
        {
            throw new BusinessException(InvalidVerificationLink());
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
        {
            throw new BusinessException(InvalidVerificationLink());
        }

        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded) throw new BusinessException(InvalidVerificationLink());
    }

    public async Task RequestPasswordResetAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ContactIdentifier.TryParse(request.EffectiveIdentifier, out var contact))
        {
            return;
        }

        if (contact.Kind == ContactKind.Phone)
        {
            await RequestPhonePasswordResetAsync(contact, cancellationToken);
            return;
        }

        if (!emailSender.IsEnabled)
        {
            throw new BusinessException(EmailUnavailable());
        }

        var user = await FindByIdentifierAsync(contact.Value, cancellationToken);
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
            return;
        }
        catch (Exception exception) when (exception is not BusinessException and not OperationCanceledException)
        {
            logger.LogWarning(exception, "Unable to send password reset email for user {UserId}.", user.Id);
            throw new BusinessException(EmailUnavailable());
        }
    }

    public async Task ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (ContactIdentifier.TryParse(request.EffectiveIdentifier, out var contact) && contact.Kind == ContactKind.Phone)
        {
            await ResetPhonePasswordAsync(contact, request, cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token) ||
            string.IsNullOrWhiteSpace(request.Password) || request.Password != request.ConfirmPassword)
        {
            throw new BusinessException(InvalidResetRequest());
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
        {
            throw new BusinessException(InvalidResetRequest());
        }

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.Password);
        if (!result.Succeeded)
        {
            throw new BusinessException(new ApplicationError(
                ErrorCode.ValidationFailed, ErrorCode.ValidationFailed.Message, ApplicationErrorType.Validation,
                ToErrors(result)));
        }

        await RevokeAllRefreshTokensAsync(user.Id, timeProvider.GetUtcNow(), cancellationToken);
        return;
    }

    private async Task RequestPhonePasswordResetAsync(
        ContactIdentifier contact,
        CancellationToken cancellationToken)
    {
        var user = await FindByIdentifierAsync(contact.Value, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var code = CreateOtp();
        var challenge = await dbContext.PasswordResetChallenges.SingleOrDefaultAsync(
            item => item.UserId == user.Id,
            cancellationToken);
        if (challenge is null)
        {
            challenge = PasswordResetOtp.Create(user.Id, contact.Value, HashOtp(code), now);
            dbContext.PasswordResetChallenges.Add(challenge);
        }
        else if (!challenge.TryResend(HashOtp(code), now))
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        try
        {
            var deliveredCode = await contactOtpSender.SendAsync(contact, code, cancellationToken);
            challenge.ReplaceCodeHash(HashOtp(deliveredCode));
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }
        catch (Exception exception) when (exception is not BusinessException and not OperationCanceledException)
        {
            logger.LogWarning(exception, "Unable to send password reset SMS for user {UserId}.", user.Id);
            dbContext.PasswordResetChallenges.Remove(challenge);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new BusinessException(SmsUnavailable());
        }
    }

    private async Task ResetPhonePasswordAsync(
        ContactIdentifier contact,
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length != 6 || !request.Code.All(char.IsAsciiDigit) ||
            string.IsNullOrWhiteSpace(request.Password) || request.Password != request.ConfirmPassword)
        {
            throw new BusinessException(InvalidResetRequest());
        }

        var user = await FindByIdentifierAsync(contact.Value, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new BusinessException(InvalidResetRequest());
        }

        var challenge = await dbContext.PasswordResetChallenges.AsNoTracking().SingleOrDefaultAsync(
            item => item.UserId == user.Id && item.Contact == contact.Value,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        var codeHash = HashOtp(request.Code);
        if (challenge is null || !challenge.IsUsableAt(now) || !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(challenge.CodeHash), Convert.FromHexString(codeHash)))
        {
            if (challenge is not null && challenge.IsUsableAt(now))
            {
                await dbContext.PasswordResetChallenges
                    .Where(item => item.Id == challenge.Id && item.ConsumedAtUtc == null && item.ExpiresAtUtc > now && item.FailedAttemptCount < 5)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.FailedAttemptCount, item => item.FailedAttemptCount + 1), cancellationToken);
            }
            throw new BusinessException(InvalidResetRequest());
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var consumed = await dbContext.PasswordResetChallenges
            .Where(item => item.Id == challenge.Id && item.ConsumedAtUtc == null && item.ExpiresAtUtc > now &&
                item.FailedAttemptCount < 5 && item.CodeHash == codeHash)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ConsumedAtUtc, now), cancellationToken);
        if (consumed != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new BusinessException(InvalidResetRequest());
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await userManager.ResetPasswordAsync(user, token, request.Password);
        if (!reset.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new BusinessException(new ApplicationError(
                ErrorCode.ValidationFailed, ErrorCode.ValidationFailed.Message, ApplicationErrorType.Validation, ToErrors(reset)));
        }

        await RevokeAllRefreshTokensAsync(user.Id, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return;
    }

    public async Task<AuthenticationResponse> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword) ||
            request.NewPassword != request.ConfirmPassword)
        {
            throw ValidationFailure(new Dictionary<string, string[]>
            {
                ["password"] = ["Current password, matching new password, and confirmation are required."]
            });
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            throw UnauthorizedFailure(
                ErrorCode.InvalidAccessToken, ErrorCode.InvalidAccessToken.Message);
        }

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            throw ValidationFailure(ToErrors(result));
        }

        var now = timeProvider.GetUtcNow();
        await RevokeOtherSessionsAsync(user.Id, null, now, cancellationToken);
        return await IssueNewTokenPairAsync(user, now, null, cancellationToken);
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

    private async Task<IReadOnlyList<string>> GetRolesAsync(User user)
    {
        if (!string.IsNullOrWhiteSpace(user.Email) && adminOptions.IsBootstrapAdmin(user.Email))
        {
            await EnsureBootstrapAdminRoleAsync(user);
        }

        return (await userManager.GetRolesAsync(user))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task EnsureBootstrapAdminRoleAsync(User user)
    {
        if (!await roleManager.RoleExistsAsync(AppRoles.Admin))
        {
            var createRole = await roleManager.CreateAsync(new IdentityRole<Guid>(AppRoles.Admin));
            if (!createRole.Succeeded && !await roleManager.RoleExistsAsync(AppRoles.Admin))
            {
                throw new InvalidOperationException("The bootstrap administrator role could not be created.");
            }
        }

        if (!await userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            var addRole = await userManager.AddToRoleAsync(user, AppRoles.Admin);
            if (!addRole.Succeeded)
            {
                throw new InvalidOperationException("The bootstrap administrator role could not be assigned.");
            }
        }
    }

    private async Task<IdentityResult> CreateUserAsync(
        User user,
        string password,
        RefreshToken refreshToken,
        AuthSession session,
        CancellationToken cancellationToken)
    {
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            dbContext.ChangeTracker.Clear();
            return result;
        }

        dbContext.AuthSessions.Add(session);
        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

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
        var sessions = await dbContext.AuthSessions.AsNoTracking()
            .Where(session => session.UserId == userId && session.RevokedAtUtc == null && session.ExpiresAtUtc > now)
            .OrderByDescending(session => session.LastSeenAtUtc)
            .Select(session => new AuthSessionResponse(session.Id, session.UserAgent, session.CreatedAtUtc,
                session.LastSeenAtUtc, session.ExpiresAtUtc, session.Id == currentSessionId))
            .ToListAsync(cancellationToken);
        return sessions;
    }

    public async Task RevokeSessionAsync(
        Guid userId, Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var session = await dbContext.AuthSessions.SingleOrDefaultAsync(
            item => item.Id == sessionId && item.UserId == userId, cancellationToken);
        if (session is null) throw new BusinessException(new ApplicationError(
            "session_not_found", "The session was not found.", ApplicationErrorType.NotFound));
        session.Revoke(now);
        await dbContext.RefreshTokens.Where(token => token.SessionId == sessionId && token.RevokedAt == null && token.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return;
    }

    public Task RevokeOtherSessionsAsync(Guid userId, Guid? currentSessionId, DateTimeOffset now,
        CancellationToken cancellationToken) =>
        RevokeSessionsAsync(userId, currentSessionId, now, cancellationToken);

    private async Task RevokeSessionsAsync(Guid userId, Guid? preservedSessionId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sessionIds = await dbContext.AuthSessions
            .Where(session => session.UserId == userId && session.RevokedAtUtc == null && session.ExpiresAtUtc > now &&
                              (preservedSessionId == null || session.Id != preservedSessionId))
            .Select(session => session.Id)
            .ToListAsync(cancellationToken);
        if (sessionIds.Count == 0) return;
        await dbContext.AuthSessions.Where(session => sessionIds.Contains(session.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAtUtc, now), cancellationToken);
        await dbContext.RefreshTokens.Where(token => sessionIds.Contains(token.SessionId) && token.RevokedAt == null && token.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);
    }

    private async Task SendEmailVerificationForUserAsync(User user, CancellationToken cancellationToken)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var verificationUrl = BuildFrontendUrl("verify", user.Email!, token);
        await emailSender.SendAsync(
            user.Email!,
            "Verify your Fookbase email",
            $"<p>Thanks for joining Fookbase.</p><p><a href=\"{verificationUrl}\">Verify email address</a></p>",
            cancellationToken);
    }

    private string BuildFrontendUrl(string mode, string email, string token) =>
        $"{emailOptions.FrontendBaseUrl.TrimEnd('/')}/login?mode={mode}&email={WebUtility.UrlEncode(email)}&token={WebUtility.UrlEncode(token)}";

    private static IReadOnlyDictionary<string, string[]> ToErrors(IdentityResult result) =>
        result.Errors
            .GroupBy(error => error.Code, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray(),
                StringComparer.Ordinal);

    private static BusinessException ValidationFailure(
        IReadOnlyDictionary<string, string[]> details) =>
        new BusinessException(
            new ApplicationError(
                ErrorCode.ValidationFailed,
                ErrorCode.ValidationFailed.Message,
                ApplicationErrorType.Validation,
                details));

    private static BusinessException ConflictFailure(string code, string message) =>
        new BusinessException(
            new ApplicationError(code, message, ApplicationErrorType.Conflict));

    private static BusinessException UnauthorizedFailure(string code, string message) =>
        new BusinessException(
            new ApplicationError(code, message, ApplicationErrorType.Unauthorized));

    private static ApplicationError InvalidRefreshToken() =>
        new(
            "invalid_refresh_token",
            "The refresh token is invalid or expired.",
            ApplicationErrorType.Unauthorized);

    private static ApplicationError EmailUnavailable() =>
        new(
            "email_unavailable",
            "Email delivery is not configured or is temporarily unavailable.",
            ApplicationErrorType.Conflict);

    private static ApplicationError SmsUnavailable() =>
        new(
            "sms_unavailable",
            "SMS delivery is not configured or is temporarily unavailable.",
            ApplicationErrorType.Conflict);

    private static ApplicationError InvalidVerificationLink() =>
        new(
            "invalid_verification_link",
            "The email verification link is invalid or expired.",
            ApplicationErrorType.Validation);

    private static ApplicationError InvalidResetRequest() =>
        new(
            "invalid_password_reset",
            "The password reset request is invalid or expired.",
            ApplicationErrorType.Validation);

    private static string CreateOtp() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private static string HashOtp(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
