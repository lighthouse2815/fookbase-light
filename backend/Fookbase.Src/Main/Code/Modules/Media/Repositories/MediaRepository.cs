using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Media.Repositories;

internal sealed class MediaRepository(MediaDbContext dbContext) : IMediaRepository
{
    public Task<MediaAsset?> FindAsync(Guid mediaId, bool trackChanges = false,
        CancellationToken cancellationToken = default)
    {
        var query = trackChanges ? dbContext.MediaAssets.AsQueryable() : dbContext.MediaAssets.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.Id == mediaId, cancellationToken);
    }

    public Task<bool> HasActiveReferencesAsync(Guid mediaId, CancellationToken cancellationToken = default) =>
        dbContext.MediaReferences.AnyAsync(x => x.MediaId == mediaId, cancellationToken);

    public async Task AddPendingAsync(MediaAsset asset, CancellationToken cancellationToken = default)
    {
        dbContext.MediaAssets.Add(asset);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveReadyAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task SaveFailedAsync(MediaAsset asset, CancellationToken cancellationToken = default)
    {
        dbContext.ObjectDeletions.Add(ObjectDeletion.Create(asset.Id, asset.ObjectKey, DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveDeletedAsync(MediaAsset asset,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken = default)
    {
        dbContext.ObjectDeletions.Add(ObjectDeletion.Create(asset.Id, asset.ObjectKey,
            deletedAtUtc));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SynchronizePostReferencesAsync(
        Guid postId,
        IReadOnlyCollection<Guid> mediaIds,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var currentReferences = await dbContext.MediaReferences
            .Where(reference => reference.PostId == postId)
            .ToListAsync(cancellationToken);
        var desiredMediaIds = mediaIds.ToHashSet();

        dbContext.MediaReferences.RemoveRange(
            currentReferences.Where(reference => !desiredMediaIds.Contains(reference.MediaId)));
        foreach (var mediaId in desiredMediaIds.Except(currentReferences.Select(reference => reference.MediaId)))
        {
            dbContext.MediaReferences.Add(MediaReference.Create(mediaId, postId, changedAtUtc));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemovePostReferencesAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        await dbContext.MediaReferences
            .Where(reference => reference.PostId == postId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
