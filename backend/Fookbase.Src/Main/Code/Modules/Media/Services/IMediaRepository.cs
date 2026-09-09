using Fookbase.Api.Modules.Media.Entities;

namespace Fookbase.Api.Modules.Media.Services;

public interface IMediaRepository
{
    Task<MediaAsset?> FindAsync(
        Guid mediaId,
        bool trackChanges = false,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveReferencesAsync(Guid mediaId, CancellationToken cancellationToken = default);

    Task AddPendingAsync(MediaAsset asset, CancellationToken cancellationToken = default);

    Task SaveReadyAsync(CancellationToken cancellationToken = default);

    Task SaveFailedAsync(
        MediaAsset asset,
        CancellationToken cancellationToken = default);

    Task SaveDeletedAsync(
        MediaAsset asset,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken = default);

    Task SynchronizePostReferencesAsync(
        Guid postId,
        IReadOnlyCollection<Guid> mediaIds,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken = default);

    Task RemovePostReferencesAsync(Guid postId, CancellationToken cancellationToken = default);
}
