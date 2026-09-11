namespace Fookbase.Api.Modules.Media.Services;

public sealed record StoredObjectInfo(long SizeBytes, string ContentType);

public interface IObjectStorage
{
    Task<string> CreatePresignedPutUrlAsync(
        string objectKey,
        TimeSpan expiry,
        CancellationToken cancellationToken = default);

    Task<string> CreatePresignedGetUrlAsync(
        string objectKey,
        TimeSpan expiry,
        CancellationToken cancellationToken = default);

    Task<StoredObjectInfo?> GetInfoAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    Task<byte[]> ReadPrefixAsync(
        string objectKey,
        int length,
        CancellationToken cancellationToken = default);

    Task DownloadToFileAsync(
        string objectKey,
        string destinationPath,
        CancellationToken cancellationToken = default);

    Task UploadFileAsync(
        string objectKey,
        string sourcePath,
        string contentType,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken = default);
}
