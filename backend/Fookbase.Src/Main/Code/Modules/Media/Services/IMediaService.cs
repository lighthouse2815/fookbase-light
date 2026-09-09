using Fookbase.Api.Modules.Media.DTOs;
using Fookbase.Api.Modules.Media.Services;

namespace Fookbase.Api.Modules.Media.Services;

public interface IMediaService
{
    Task<ApplicationResult<UploadIntentResponse>> CreateUploadAsync(
        Guid ownerUserId,
        CreateUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<ApplicationResult<MediaResponse>> CompleteAsync(
        Guid ownerUserId,
        Guid mediaId,
        CancellationToken cancellationToken = default);

    Task<ApplicationResult<MediaResponse>> GetMetadataAsync(
        Guid ownerUserId,
        Guid mediaId,
        CancellationToken cancellationToken = default);

    Task<ApplicationResult<MediaReadUrlResponse>> CreateReadUrlAsync(
        Guid mediaId,
        CancellationToken cancellationToken = default);

    Task<ApplicationResult> ValidatePostMediaAsync(
        Guid ownerUserId,
        IReadOnlyCollection<Guid> mediaIds,
        CancellationToken cancellationToken = default);

    Task<ApplicationResult> SynchronizePostReferencesAsync(
        Guid ownerUserId,
        Guid postId,
        IReadOnlyCollection<Guid> mediaIds,
        CancellationToken cancellationToken = default);

    Task RemovePostReferencesAsync(Guid postId, CancellationToken cancellationToken = default);

    Task<ApplicationResult> DeleteAsync(
        Guid ownerUserId,
        Guid mediaId,
        CancellationToken cancellationToken = default);
}
