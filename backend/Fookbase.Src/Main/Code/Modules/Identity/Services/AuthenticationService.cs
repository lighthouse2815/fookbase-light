using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Persistence;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Identity.Config;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class AuthenticationService(
    UserManager<User> userManager,
    FookbaseDbContext dbContext,
    JwtTokenService tokenService,
    RoleManager<IdentityRole<Guid>> roleManager,
    IEmailSender emailSender,
    EmailOptions emailOptions,
    AdminOptions adminOptions,
    ILogger<AuthenticationService> logger,
    TimeProvider timeProvider)
{
    public async Task<ApplicationResult<AuthenticationResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = AuthenticationValidation.Validate(request);
        if (validationErrors.Count > 0)
        {
            return ValidationFailure<AuthenticationResponse>(validationErrors);
        }

        var email = request.Email!.Trim();
        var userName = request.Username!.Trim();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return ConflictFailure<AuthenticationResponse>(
                "duplicate_email",
                "An account with this email already exists.");
        }

        if (await userManager.FindByNameAsync(userName) is not null)
        {
            return ConflictFailure<AuthenticationResponse>(
                "duplicate_username",
                "An account with this username already exists.");
        }

        var now = timeProvider.GetUtcNow();
        var user = new User(Guid.NewGuid(), email, userName, now);
        var refreshToken = tokenService.CreateRefreshToken(user.Id, now);
        var creationResult = await CreateUserAsync(
            user,
            request.Password!,
            refreshToken.RefreshToken,
            cancellationToken);

        if (!creationResult.Succeeded)
        {
            var errors = ToErrors(creationResult);
            if (errors.ContainsKey("DuplicateEmail"))
            {
                return ConflictFailure<AuthenticationResponse>(
                    "duplicate_email",
                    "An account with this email already exists.");
            }

            if (errors.ContainsKey("DuplicateUserName"))
            {
                return ConflictFailure<AuthenticationResponse>(
                    "duplicate_username",
                    "An account with this username already exists.");
            }

            return ValidationFailure<AuthenticationResponse>(errors);
        }

        if (emailSender.IsEnabled)
        {
            try
            {
                await SendEmailVerificationForUserAsync(user, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Unable to send email verification for user {UserId}.", user.Id);
            }
        }

        var roles = await GetRolesAsync(user);
        return ApplicationResult<AuthenticationResponse>.Success(
            BuildResponse(user, roles, tokenService.CreateAccessToken(user, roles, now), refreshToken));
    }

    public async Task<ApplicationResult<AuthenticationResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = AuthenticationValidation.Validate(request);
        if (validationErrors.Count > 0)
        {
            return ValidationFailure<AuthenticationResponse>(validationErrors);
        }

        var user = await userManager.FindByEmailAsync(request.Email!.Trim());
        if (user is null || !user.IsActive ||
            !await userManager.CheckPasswordAsync(user, request.Password!))
        {
            return UnauthorizedFailure<AuthenticationResponse>(
                "invalid_credentials",
                "The email or password is invalid.");
        }

        return await IssueNewTokenPairAsync(
            user,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public async Task<ApplicationResult<AuthenticationResponse>> RefreshAsync(
        RefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return UnauthorizedFailure<AuthenticationResponse>(
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
            return UnauthorizedFailure<AuthenticationResponse>(
                "invalid_refresh_token",
                "The refresh token is invalid or expired.");
        }

        var user = await userManager.FindByIdAsync(currentToken.UserId.ToString());
        if (user is null || !user.IsActive)
        {
            return UnauthorizedFailure<AuthenticationResponse>(
                "invalid_refresh_token",
                "The refresh token is invalid or expired.");
        }

        var replacement = tokenService.CreateRefreshToken(user.Id, now);
        var rotated = await RotateRefreshTokenAsync(
            currentToken.Id,
            replacement.RefreshToken,
            now,
            cancellationToken);

        if (!rotated)
        {
            return UnauthorizedFailure<AuthenticationResponse>(
                "invalid_refresh_token",
                "The refresh token is invalid or expired.");
        }

        var roles = await GetRolesAsync(user);
        return ApplicationResult<AuthenticationResponse>.Success(
            BuildResponse(user, roles, tokenService.CreateAccessToken(user, roles, now), replacement));
    }

    public async Task<ApplicationResult> LogoutAsync(
        Guid userId,
        LogoutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return ApplicationResult.Failure(InvalidRefreshToken());
        }

        var revoked = await RevokeRefreshTokenAsync(
            tokenService.HashRefreshToken(request.RefreshToken),
            userId,
            timeProvider.GetUtcNow(),
            cancellationToken);

        return revoked
            ? ApplicationResult.Success()
            : ApplicationResult.Failure(InvalidRefreshToken());
    }

    public async Task<ApplicationResult<AuthenticatedUserResponse>> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return UnauthorizedFailure<AuthenticatedUserResponse>(
                "invalid_access_token",
                "The access token is invalid.");
        }

        return ApplicationResult<AuthenticatedUserResponse>.Success(
            ToResponse(user, await GetRolesAsync(user)));
    }

    public async Task<ApplicationResult> SendEmailVerificationAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return ApplicationResult.Failure(new ApplicationError(
                "invalid_access_token", "The access token is invalid.", ApplicationErrorType.Unauthorized));
        }

        if (user.EmailConfirmed)
        {
            return ApplicationResult.Success();
        }

        if (!emailSender.IsEnabled)
        {
            return ApplicationResult.Failure(EmailUnavailable());
        }

        try
        {
            await SendEmailVerificationForUserAsync(user, cancellationToken);
            return ApplicationResult.Success();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to resend email verification for user {UserId}.", user.Id);
            return ApplicationResult.Failure(EmailUnavailable());
        }
    }

    public async Task<ApplicationResult> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token))
        {
            return ApplicationResult.Failure(InvalidVerificationLink());
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
        {
            return ApplicationResult.Failure(InvalidVerificationLink());
        }

        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        return result.Succeeded
            ? ApplicationResult.Success()
            : ApplicationResult.Failure(InvalidVerificationLink());
    }

    public async Task<ApplicationResult> RequestPasswordResetAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!emailSender.IsEnabled)
        {
            return ApplicationResult.Failure(EmailUnavailable());
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return ApplicationResult.Success();
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
        {
            return ApplicationResult.Success();
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
            return ApplicationResult.Success();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to send password reset email for user {UserId}.", user.Id);
            return ApplicationResult.Failure(EmailUnavailable());
        }
    }

    public async Task<ApplicationResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token) ||
            string.IsNullOrWhiteSpace(request.Password) || request.Password != request.ConfirmPassword)
        {
            return ApplicationResult.Failure(InvalidResetRequest());
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
        {
            return ApplicationResult.Failure(InvalidResetRequest());
        }

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.Password);
        if (!result.Succeeded)
        {
            return ApplicationResult.Failure(new ApplicationError(
                "validation_failed", "One or more validation errors occurred.", ApplicationErrorType.Validation,
                ToErrors(result)));
        }

        await RevokeAllRefreshTokensAsync(user.Id, timeProvider.GetUtcNow(), cancellationToken);
        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult<AuthenticationResponse>> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword) ||
            request.NewPassword != request.ConfirmPassword)
        {
            return ValidationFailure<AuthenticationResponse>(new Dictionary<string, string[]>
            {
                ["password"] = ["Current password, matching new password, and confirmation are required."]
            });
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return UnauthorizedFailure<AuthenticationResponse>(
                "invalid_access_token", "The access token is invalid.");
        }

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return ValidationFailure<AuthenticationResponse>(ToErrors(result));
        }

        var now = timeProvider.GetUtcNow();
        await RevokeAllRefreshTokensAsync(user.Id, now, cancellationToken);
        return await IssueNewTokenPairAsync(user, now, cancellationToken);
    }

    private async Task<ApplicationResult<AuthenticationResponse>> IssueNewTokenPairAsync(
        User user,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var roles = await GetRolesAsync(user);
        var accessToken = tokenService.CreateAccessToken(user, roles, now);
        var refreshToken = tokenService.CreateRefreshToken(user.Id, now);

        dbContext.RefreshTokens.Add(refreshToken.RefreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<AuthenticationResponse>.Success(
            BuildResponse(user, roles, accessToken, refreshToken));
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
        new(user.Id, user.Email!, user.UserName!, user.EmailConfirmed, roles);

    private async Task<IReadOnlyList<string>> GetRolesAsync(User user)
    {
        if (adminOptions.IsBootstrapAdmin(user.Email!))
        {
            await EnsureBootstrapAdminRoleAsync(user);
        }

        return (await userManager.GetRolesAsync(user))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task EnsureBootstrapAdminRoleAsync(User user)
    {
        if (!await roleManager.RoleExistsAsync(AdminRole.Name))
        {
            var createRole = await roleManager.CreateAsync(new IdentityRole<Guid>(AdminRole.Name));
            if (!createRole.Succeeded && !await roleManager.RoleExistsAsync(AdminRole.Name))
            {
                throw new InvalidOperationException("The bootstrap administrator role could not be created.");
            }
        }

        if (!await userManager.IsInRoleAsync(user, AdminRole.Name))
        {
            var addRole = await userManager.AddToRoleAsync(user, AdminRole.Name);
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
        CancellationToken cancellationToken)
    {
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            dbContext.ChangeTracker.Clear();
            return result;
        }

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

    private async Task<bool> RevokeRefreshTokenAsync(
        string tokenHash,
        Guid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken) =>
        await dbContext.RefreshTokens
            .Where(token => token.TokenHash == tokenHash && token.UserId == userId &&
                            token.RevokedAt == null && token.ExpiresAt > revokedAt)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAt, revokedAt),
                cancellationToken) == 1;

    private Task RevokeAllRefreshTokensAsync(
        Guid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken) =>
        dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null && token.ExpiresAt > revokedAt)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAt, revokedAt),
                cancellationToken);

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

    private static ApplicationResult<T> ValidationFailure<T>(
        IReadOnlyDictionary<string, string[]> details) =>
        ApplicationResult<T>.Failure(
            new ApplicationError(
                "validation_failed",
                "One or more validation errors occurred.",
                ApplicationErrorType.Validation,
                details));

    private static ApplicationResult<T> ConflictFailure<T>(string code, string message) =>
        ApplicationResult<T>.Failure(
            new ApplicationError(code, message, ApplicationErrorType.Conflict));

    private static ApplicationResult<T> UnauthorizedFailure<T>(string code, string message) =>
        ApplicationResult<T>.Failure(
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
}
