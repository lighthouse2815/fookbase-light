using Fookbase.Api.Modules.Admin.Services;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Domain.Enums;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class AuthenticationService(
    UserManager<User> userManager,
    FookbaseDbContext dbContext,
    JwtTokenService tokenService,
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
        IssueSessionAsync(user, timeProvider.GetUtcNow(), userAgent, cancellationToken);

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
        return await IssueSessionAsync(user, now, userAgent, cancellationToken);
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

    internal async Task<bool> VerifyAuthenticatorCodeAsync(User user, string? code) =>
        !string.IsNullOrWhiteSpace(code) && await userManager.VerifyTwoFactorTokenAsync(user,
            TokenOptions.DefaultAuthenticatorProvider, code.Replace(" ", string.Empty).Replace("-", string.Empty));

    internal Task<User?> FindByIdentifierAsync(
        ContactIdentifier contact,
        CancellationToken cancellationToken = default) =>
        contact.Kind == ContactKind.Email
            ? userManager.FindByEmailAsync(contact.Value)
            : dbContext.Users.SingleOrDefaultAsync(user => user.PhoneNumber == contact.Value, cancellationToken);

    internal async Task<User> GetActiveUserAsync(Guid userId)
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
            return await IssueSessionAsync(user, now, userAgent, cancellationToken);
        }

        var challenge = new TwoFactorLoginChallenge(user.Id, now, pendingExternalProvider, pendingExternalProviderKey);
        dbContext.TwoFactorLoginChallenges.Add(challenge);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new TwoFactorChallengeResponse(true, challenge.Id.ToString("N"), challenge.ExpiresAtUtc);
    }

    internal async Task<AuthenticationResponse> IssueSessionAsync(
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

    private static BusinessException Failure(
        ErrorCode errorCode,
        ApplicationErrorType type = ApplicationErrorType.Unauthorized,
        IReadOnlyDictionary<string, string[]>? details = null) =>
        new(new ApplicationError(errorCode.Code, errorCode.Message, type, details));
}
