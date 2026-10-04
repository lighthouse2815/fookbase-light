using Fookbase.Api.Modules.Media.Domain.Enums;
using Fookbase.Api.Modules.Media.DTOs.Responses;

namespace Fookbase.Api.Modules.Media.Abstractions;

public interface IObjectStorage
{
    Task<DirectUploadIntent> CreateDirectUploadIntentAsync(
        string objectKey,
        MediaType mediaType,
        TimeSpan expiry,
        CancellationToken cancellationToken = default);

    Task<string> CreateSignedGetUrlAsync(
        string objectKey,
        MediaType mediaType,
        CancellationToken cancellationToken = default);

    Task<StoredObjectInfo?> GetInfoAsync(
        string objectKey,
        MediaType mediaType,
        CancellationToken cancellationToken = default);

    Task<byte[]> ReadPrefixAsync(
        string objectKey,
        MediaType mediaType,
        int length,
        CancellationToken cancellationToken = default);

    Task DownloadToFileAsync(
        string objectKey,
        MediaType mediaType,
        string destinationPath,
        CancellationToken cancellationToken = default);

    Task UploadFileAsync(
        string objectKey,
        MediaType mediaType,
        string sourcePath,
        string contentType,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string objectKey, MediaType mediaType,
        CancellationToken cancellationToken = default);
}
