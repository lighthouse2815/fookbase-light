using System.Text.Json;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Data;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class AuthenticationService(
    UserManager<User> userManager,
    IdentityDbContext dbContext,
    JwtTokenService tokenService,
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
        var accessToken = tokenService.CreateAccessToken(user, now);
        var refreshToken = tokenService.CreateRefreshToken(user.Id, now);
        var integrationEvent = new UserRegisteredIntegrationEvent(
            Guid.NewGuid(),
            user.Id,
            userName,
            now);
        var creationResult = await CreateUserAsync(
            user,
            request.Password!,
            refreshToken.RefreshToken,
            integrationEvent,
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

        return ApplicationResult<AuthenticatedUserResponse>.Success(ToResponse(user));
    }

    private async Task<ApplicationResult<AuthenticationResponse>> IssueNewTokenPairAsync(
        User user,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var accessToken = tokenService.CreateAccessToken(user, now);
        var refreshToken = tokenService.CreateRefreshToken(user.Id, now);

        dbContext.RefreshTokens.Add(refreshToken.RefreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

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

    private async Task<IdentityResult> CreateUserAsync(
        User user,
        string password,
        RefreshToken refreshToken,
        UserRegisteredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return result;
        }

        dbContext.RefreshTokens.Add(refreshToken);
        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            integrationEvent.EventId,
            UserRegisteredIntegrationEvent.EventType,
            JsonSerializer.Serialize(integrationEvent),
            integrationEvent.OccurredAtUtc));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
}
