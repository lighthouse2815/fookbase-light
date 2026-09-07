using Fookbase.Media.Application.Abstractions;
using Fookbase.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Media.Infrastructure.Persistence;

internal sealed class MediaRepository(MediaDbContext dbContext) : IMediaRepository
{
    public Task<MediaAsset?> FindAsync(
        Guid mediaId,
        bool trackChanges = false,
        CancellationToken cancellationToken = default)
    {
        var query = trackChanges
            ? dbContext.MediaAssets.AsQueryable()
            : dbContext.MediaAssets.AsNoTracking();
        return query.SingleOrDefaultAsync(asset => asset.Id == mediaId, cancellationToken);
    }

    public async Task AddAsync(
        MediaAsset asset,
        CancellationToken cancellationToken = default)
    {
        await dbContext.MediaAssets.AddAsync(asset, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
