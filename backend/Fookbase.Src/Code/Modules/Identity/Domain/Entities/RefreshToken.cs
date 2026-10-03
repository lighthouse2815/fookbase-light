using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Identity.Entities;

[Index(nameof(TokenHash), IsUnique = true)]
[Index(nameof(UserId), nameof(ExpiresAt))]
[Index(nameof(SessionId), nameof(ExpiresAt))]
public sealed class RefreshToken
{
    private RefreshToken(){}

    public RefreshToken(
        Guid id,
        Guid userId,
        string tokenHash,
        Guid sessionId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        SessionId = sessionId;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public User User { get; private set; } = null!;

    [MaxLength(64)]
    public string TokenHash { get; private set; } = string.Empty;

    public Guid SessionId { get; private set; }

    public AuthSession Session { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public RefreshToken? ReplacedByToken { get; private set; }

    public bool IsActiveAt(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset revokedAt, Guid? replacedByTokenId = null)
    {
        if (RevokedAt is not null)
        {
            throw new InvalidOperationException("refresh token đã được revoked trước đó rồi");
        }

        RevokedAt = revokedAt;
        ReplacedByTokenId = replacedByTokenId;
    }
}
