using Fookbase.Api.Modules.Identity.Abstractions;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Config;
using Fookbase.Api.Modules.Identity.Domain.Enums;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class AccountSecurityService(
    UserManager<User> userManager,
    FookbaseDbContext dbContext,
    AuthenticationService authenticationService,
    IEmailSender emailSender,
    OtpService otpService,
    EmailOptions emailOptions,
    ILogger<AccountSecurityService> logger,
    TimeProvider timeProvider)
{
    public async Task<SecurityStateResponse> GetSecurityAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await authenticationService.GetActiveUserAsync(userId);
        var now = timeProvider.GetUtcNow();
        return new SecurityStateResponse(user.TwoFactorEnabled,
            await userManager.CountRecoveryCodesAsync(user),
            await dbContext.AuthSessions.CountAsync(session => session.UserId == userId && session.RevokedAtUtc == null && session.ExpiresAtUtc > now, cancellationToken));
    }

    public async Task<TwoFactorSetupResponse> SetupTwoFactorAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await authenticationService.GetActiveUserAsync(userId);
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
        var user = await authenticationService.GetActiveUserAsync(userId);
        if (!await authenticationService.VerifyAuthenticatorCodeAsync(user, code))
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
        var user = await authenticationService.GetActiveUserAsync(userId);
        if (string.IsNullOrWhiteSpace(currentPassword) || !await userManager.CheckPasswordAsync(user, currentPassword))
            throw Failure(ErrorCode.InvalidCredentials, ApplicationErrorType.Validation);
        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        await authenticationService.RevokeOtherSessionsAsync(userId, null, timeProvider.GetUtcNow(), cancellationToken);
    }

    public async Task SendEmailVerificationAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await authenticationService.GetActiveUserAsync(userId);

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
            var phoneUser = await authenticationService.FindByIdentifierAsync(contact, cancellationToken);
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

        var user = await authenticationService.FindByIdentifierAsync(contact, cancellationToken);
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

        var user = await authenticationService.FindByIdentifierAsync(contact, cancellationToken);
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

        var user = await authenticationService.FindByIdentifierAsync(contact, cancellationToken);
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

        var user = await authenticationService.GetActiveUserAsync(userId);

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            throw Failure(ErrorCode.ValidationFailed, ApplicationErrorType.Validation, ToErrors(result));
        }

        var now = timeProvider.GetUtcNow();
        await authenticationService.RevokeOtherSessionsAsync(user.Id, null, now, cancellationToken);
        return await authenticationService.IssueSessionAsync(user, now, null, cancellationToken);
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
