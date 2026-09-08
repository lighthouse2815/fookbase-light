namespace Fookbase.Api.Modules.Identity.Models;

public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    private RefreshToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public static RefreshToken Create(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt) =>
        new(id, userId, tokenHash, createdAt, expiresAt);

    public bool IsActiveAt(DateTimeOffset now) =>
        RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset revokedAt, Guid? replacedByTokenId = null)
    {
        if (RevokedAt is not null)
        {
            throw new InvalidOperationException("The refresh token is already revoked.");
        }

        RevokedAt = revokedAt;
        ReplacedByTokenId = replacedByTokenId;
    }
}
