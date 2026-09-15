using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Media.Services;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class FakeObjectStorage : IObjectStorage
{
    public Task<DirectUploadIntent> CreateDirectUploadIntentAsync(
        string objectKey,
        MediaType mediaType,
        TimeSpan expiry,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new DirectUploadIntent($"https://storage.test/{objectKey}?upload=1", new Dictionary<string, string>()));

    public Task<string> CreateSignedGetUrlAsync(string objectKey, MediaType mediaType,
        CancellationToken cancellationToken = default) =>
        Task.FromResult($"https://storage.test/{objectKey}?signed=1");

    public Task<StoredObjectInfo?> GetInfoAsync(string objectKey, MediaType mediaType,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<StoredObjectInfo?>(null);

    public Task<byte[]> ReadPrefixAsync(string objectKey, MediaType mediaType, int length,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Array.Empty<byte>());

    public Task DownloadToFileAsync(
        string objectKey,
        MediaType mediaType,
        string destinationPath,
        CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException("Test storage contains no video objects."));

    public Task UploadFileAsync(
        string objectKey,
        MediaType mediaType,
        string sourcePath,
        string contentType,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteAsync(string objectKey, MediaType mediaType,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
