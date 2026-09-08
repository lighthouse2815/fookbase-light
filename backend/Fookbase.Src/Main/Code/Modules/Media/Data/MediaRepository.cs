using System.Text.Json;
using Fookbase.Api.Shared.Contracts.Media;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Media.Models;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Media.Data;

internal sealed class MediaRepository(MediaDbContext dbContext) : IMediaRepository
{
    public Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.KnownUsers.AnyAsync(x => x.UserId == userId, cancellationToken);

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

    public Task SaveReadyAsync(MediaAsset asset, MediaReadyIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default) =>
        SaveWithOutboxAsync(integrationEvent.EventId, MediaReadyIntegrationEvent.EventType,
            integrationEvent, integrationEvent.OccurredAtUtc, cancellationToken);

    public async Task SaveFailedAsync(MediaAsset asset, CancellationToken cancellationToken = default)
    {
        dbContext.ObjectDeletions.Add(ObjectDeletion.Create(asset.Id, asset.ObjectKey, DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveDeletedAsync(MediaAsset asset, MediaDeletedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        AddOutbox(integrationEvent.EventId, MediaDeletedIntegrationEvent.EventType,
            integrationEvent, integrationEvent.OccurredAtUtc);
        dbContext.ObjectDeletions.Add(ObjectDeletion.Create(asset.Id, asset.ObjectKey,
            integrationEvent.OccurredAtUtc));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task SaveWithOutboxAsync(Guid id, string type, object payload,
        DateTimeOffset occurredAtUtc, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        AddOutbox(id, type, payload, occurredAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private void AddOutbox(Guid id, string type, object payload, DateTimeOffset occurredAtUtc) =>
        dbContext.OutboxMessages.Add(OutboxMessage.Create(id, type,
            JsonSerializer.Serialize(payload, payload.GetType()), occurredAtUtc));
}
