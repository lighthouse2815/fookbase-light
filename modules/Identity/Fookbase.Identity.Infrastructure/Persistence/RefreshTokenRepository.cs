using Fookbase.Identity.Application.Abstractions;
using Fookbase.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Identity.Infrastructure.Persistence;

internal sealed class RefreshTokenRepository(IdentityDbContext dbContext)
    : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default) =>
        dbContext.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(
                token => token.TokenHash == tokenHash,
                cancellationToken);

    public async Task AddAsync(
        RefreshToken refreshToken,
        CancellationToken cancellationToken = default)
    {
        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RotateAsync(
        Guid currentTokenId,
        RefreshToken replacement,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        dbContext.RefreshTokens.Add(replacement);
        await dbContext.SaveChangesAsync(cancellationToken);

        var updatedRows = await dbContext.RefreshTokens
            .Where(token =>
                token.Id == currentTokenId &&
                token.RevokedAt == null &&
                token.ExpiresAt > revokedAt)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.RevokedAt, revokedAt)
                    .SetProperty(token => token.ReplacedByTokenId, replacement.Id),
                cancellationToken);

        if (updatedRows != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.Entry(replacement).State = EntityState.Detached;
            return false;
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RevokeAsync(
        string tokenHash,
        Guid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default)
    {
        var updatedRows = await dbContext.RefreshTokens
            .Where(token =>
                token.TokenHash == tokenHash &&
                token.UserId == userId &&
                token.RevokedAt == null &&
                token.ExpiresAt > revokedAt)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAt, revokedAt),
                cancellationToken);

        return updatedRows == 1;
    }
}
