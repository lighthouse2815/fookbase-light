using Fookbase.Identity.Application.Abstractions;
using Fookbase.Identity.Application.Common;
using Fookbase.Identity.Domain.Entities;
using Fookbase.Contracts.Identity;

namespace Fookbase.Identity.Application.Authentication;

public sealed class AuthenticationService(
    IUserAccountService userAccountService,
    IUserRegistrationStore userRegistrationStore,
    IRefreshTokenRepository refreshTokenRepository,
    ITokenService tokenService,
    TimeProvider timeProvider) : IAuthenticationService
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

        if (await userAccountService.FindByEmailAsync(email) is not null)
        {
            return ConflictFailure<AuthenticationResponse>(
                "duplicate_email",
                "An account with this email already exists.");
        }

        if (await userAccountService.FindByUserNameAsync(userName) is not null)
        {
            return ConflictFailure<AuthenticationResponse>(
                "duplicate_username",
                "An account with this username already exists.");
        }

        var now = timeProvider.GetUtcNow();
        var user = new User(Guid.NewGuid(), email, userName, now);
        var accessToken = tokenService.CreateAccessToken(user, now);
        var refreshToken = tokenService.CreateRefreshToken(user.Id, now);
        var integrationEvent = new UserRegisteredIntegrationEvent(
            Guid.NewGuid(),
            user.Id,
            userName,
            now);
        var creationResult = await userRegistrationStore.CreateAsync(
            user,
            request.Password!,
            refreshToken.RefreshToken,
            integrationEvent,
            cancellationToken);

        if (!creationResult.Succeeded)
        {
            if (creationResult.Errors.ContainsKey("DuplicateEmail"))
            {
                return ConflictFailure<AuthenticationResponse>(
                    "duplicate_email",
                    "An account with this email already exists.");
            }

            if (creationResult.Errors.ContainsKey("DuplicateUserName"))
            {
                return ConflictFailure<AuthenticationResponse>(
                    "duplicate_username",
                    "An account with this username already exists.");
            }

            return ValidationFailure<AuthenticationResponse>(creationResult.Errors);
        }

        return ApplicationResult<AuthenticationResponse>.Success(
            BuildResponse(user, accessToken, refreshToken));
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

        var user = await userAccountService.FindByEmailAsync(request.Email!.Trim());
        if (user is null || !user.IsActive ||
            !await userAccountService.CheckPasswordAsync(user, request.Password!))
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
        var currentToken = await refreshTokenRepository.FindByHashAsync(
            tokenHash,
            cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (currentToken is null || !currentToken.IsActiveAt(now))
        {
            return UnauthorizedFailure<AuthenticationResponse>(
                "invalid_refresh_token",
                "The refresh token is invalid or expired.");
        }

        var user = await userAccountService.FindByIdAsync(currentToken.UserId);
        if (user is null || !user.IsActive)
        {
            return UnauthorizedFailure<AuthenticationResponse>(
                "invalid_refresh_token",
                "The refresh token is invalid or expired.");
        }

        var replacement = tokenService.CreateRefreshToken(user.Id, now);
        var rotated = await refreshTokenRepository.RotateAsync(
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

        return ApplicationResult<AuthenticationResponse>.Success(
            BuildResponse(user, tokenService.CreateAccessToken(user, now), replacement));
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

        var revoked = await refreshTokenRepository.RevokeAsync(
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
        var user = await userAccountService.FindByIdAsync(userId);
        if (user is null || !user.IsActive)
        {
            return UnauthorizedFailure<AuthenticatedUserResponse>(
                "invalid_access_token",
                "The access token is invalid.");
        }

        return ApplicationResult<AuthenticatedUserResponse>.Success(ToResponse(user));
    }

    private async Task<ApplicationResult<AuthenticationResponse>> IssueNewTokenPairAsync(
        User user,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var accessToken = tokenService.CreateAccessToken(user, now);
        var refreshToken = tokenService.CreateRefreshToken(user.Id, now);

        await refreshTokenRepository.AddAsync(refreshToken.RefreshToken, cancellationToken);

        return ApplicationResult<AuthenticationResponse>.Success(
            BuildResponse(user, accessToken, refreshToken));
    }

    private static AuthenticationResponse BuildResponse(
        User user,
        AccessTokenResult accessToken,
        RefreshTokenResult refreshToken) =>
        new(
            ToResponse(user),
            accessToken.Token,
            accessToken.ExpiresAt,
            refreshToken.RawToken,
            refreshToken.RefreshToken.ExpiresAt);

    private static AuthenticatedUserResponse ToResponse(User user) =>
        new(user.Id, user.Email!, user.UserName!);

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
}
