using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Fookbase.Api.Modules.Identity.Services.Abstractions;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Api.Modules.Identity.Services.Authentication;

internal sealed class JwtTokenService(JwtOptions options) : ITokenService
{
    public AccessTokenResult CreateAccessToken(User user, DateTimeOffset now)
    {
        var expiresAt = now.AddMinutes(options.AccessTokenExpirationMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
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

    public RefreshTokenResult CreateRefreshToken(Guid userId, DateTimeOffset now)
    {
        var rawToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        var refreshToken = RefreshToken.Create(
            Guid.NewGuid(),
            userId,
            HashRefreshToken(rawToken),
            now,
            now.AddDays(options.RefreshTokenExpirationDays));

        return new RefreshTokenResult(rawToken, refreshToken);
    }

    public string HashRefreshToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
