using Fookbase.Api.Modules.Identity.Entities;

namespace Fookbase.Api.Modules.Identity.Services.Abstractions;

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(User user, DateTimeOffset now);

    RefreshTokenResult CreateRefreshToken(Guid userId, DateTimeOffset now);

    string HashRefreshToken(string rawToken);
}

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);

public sealed record RefreshTokenResult(
    string RawToken,
    RefreshToken RefreshToken);
