namespace Fookbase.Api.Shared.ErrorHandling;

public sealed record ErrorCode(string Code, string Message)
{
    public static readonly ErrorCode ValidationFailed = new(
        "validation_failed",
        "One or more validation errors occurred.");
    public static readonly ErrorCode InvalidRequest = new(
        "invalid_request",
        "The request body is invalid.");
    public static readonly ErrorCode InternalServerError = new(
        "internal_server_error",
        "An unexpected error occurred.");
    public static readonly ErrorCode Forbidden = new(
        "forbidden",
        "You are not allowed to perform this operation.");
    public static readonly ErrorCode InvalidAccessToken = new(
        "invalid_access_token",
        "The access token is invalid.");
    public static readonly ErrorCode InvalidCredentials = new(
        "invalid_credentials",
        "The credentials are invalid.");
    public static readonly ErrorCode InvalidPagination = new(
        "invalid_pagination",
        "The pagination parameters are invalid.");
    public static readonly ErrorCode DuplicateEmail = new(
        "duplicate_email",
        "An account with this email already exists.");
    public static readonly ErrorCode DuplicateUsername = new(
        "duplicate_username",
        "An account with this username already exists.");
    public static readonly ErrorCode InvalidExternalLogin = new(
        "invalid_external_login",
        "The external login is unavailable.");
    public static readonly ErrorCode InvalidTwoFactorChallenge = new(
        "invalid_two_factor_challenge",
        "The two-factor challenge is invalid or expired.");
    public static readonly ErrorCode InvalidTwoFactorCode = new(
        "invalid_two_factor_code",
        "The two-factor code is invalid.");
    public static readonly ErrorCode InvalidRefreshToken = new(
        "invalid_refresh_token",
        "The refresh token is invalid or expired.");
    public static readonly ErrorCode TwoFactorSetupFailed = new(
        "two_factor_setup_failed",
        "Two-factor setup could not be initialized.");
    public static readonly ErrorCode TwoFactorEnableFailed = new(
        "two_factor_enable_failed",
        "Two-factor authentication could not be enabled.");
    public static readonly ErrorCode TwoFactorNotEnabled = new(
        "two_factor_not_enabled",
        "Two-factor authentication is not enabled.");
    public static readonly ErrorCode SessionNotFound = new(
        "session_not_found",
        "The session was not found.");
    public static readonly ErrorCode EmailUnavailable = new(
        "email_unavailable",
        "Email delivery is not configured or is temporarily unavailable.");
    public static readonly ErrorCode SmsUnavailable = new(
        "sms_unavailable",
        "SMS delivery is not configured or is temporarily unavailable.");
    public static readonly ErrorCode InvalidVerificationLink = new(
        "invalid_verification_link",
        "The email verification link is invalid or expired.");
    public static readonly ErrorCode InvalidPasswordReset = new(
        "invalid_password_reset",
        "The password reset request is invalid or expired.");

    public static implicit operator string(ErrorCode errorCode) => errorCode.Code;
}
