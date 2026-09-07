using Fookbase.Media.Application.Common;

namespace Fookbase.Media.Application.Media;

public interface IMediaService
{
    Task<ApplicationResult<MediaResponse>> UploadAsync(
        Guid ownerUserId,
        Stream content,
        string fileName,
        string contentType,
        long length,
        string purpose,
        CancellationToken cancellationToken = default);

    Task<ApplicationResult<MediaDownload>> DownloadAsync(
        Guid mediaId,
        CancellationToken cancellationToken = default);

    Task<ApplicationResult> DeleteAsync(
        Guid ownerUserId,
        Guid mediaId,
        CancellationToken cancellationToken = default);
}
