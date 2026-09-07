using Fookbase.Media.Domain.Entities;

namespace Fookbase.Media.Application.Abstractions;

public interface IMediaRepository
{
    Task<MediaAsset?> FindAsync(
        Guid mediaId,
        bool trackChanges = false,
        CancellationToken cancellationToken = default);

    Task AddAsync(MediaAsset asset, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
