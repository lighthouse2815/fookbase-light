using Fookbase.Contracts.Media;
using Fookbase.Media.Domain.Entities;

namespace Fookbase.Media.Application.Abstractions;

public interface IMediaRepository
{
    Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<MediaAsset?> FindAsync(
        Guid mediaId,
        bool trackChanges = false,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveReferencesAsync(Guid mediaId, CancellationToken cancellationToken = default);

    Task AddPendingAsync(MediaAsset asset, CancellationToken cancellationToken = default);

    Task SaveReadyAsync(
        MediaAsset asset,
        MediaReadyIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default);

    Task SaveFailedAsync(
        MediaAsset asset,
        CancellationToken cancellationToken = default);

    Task SaveDeletedAsync(
        MediaAsset asset,
        MediaDeletedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default);
}
