using Fookbase.Api.Modules.Identity.Config;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class JwtTokenService(JwtOptions options)
{
    public AccessTokenResult CreateAccessToken(
        User user,
        IEnumerable<string> roles,
        DateTimeOffset now,
        Guid sessionId)
    {
        var expiresAt = now.AddMinutes(options.AccessTokenExpirationMinutes);
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ,new Claim("sid", sessionId.ToString())
        };
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }
        else if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            claims.Add(new Claim(ClaimTypes.MobilePhone, user.PhoneNumber));
        }
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AccessTokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);
    }

    public RefreshTokenResult CreateRefreshToken(Guid userId, Guid sessionId, DateTimeOffset now)
    {
        var rawToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        var refreshToken = RefreshToken.Create(
            Guid.NewGuid(),
            userId,
            HashRefreshToken(rawToken),
            sessionId,
            now,
            now.AddDays(options.RefreshTokenExpirationDays));

        return new RefreshTokenResult(rawToken, refreshToken);
    }

    public string HashRefreshToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);

public sealed record RefreshTokenResult(string RawToken, RefreshToken RefreshToken);
