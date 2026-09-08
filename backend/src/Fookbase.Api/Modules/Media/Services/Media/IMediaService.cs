using Fookbase.Api.Modules.Media.Services.Common;

namespace Fookbase.Api.Modules.Media.Services.Media;

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

    Task<ApplicationResult> DeleteAsync(
        Guid ownerUserId,
        Guid mediaId,
        CancellationToken cancellationToken = default);
}
