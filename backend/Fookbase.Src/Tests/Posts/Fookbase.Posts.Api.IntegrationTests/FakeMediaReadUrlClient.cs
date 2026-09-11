using Fookbase.Api.Modules.Media.Services;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class FakeObjectStorage : IObjectStorage
{
    public Task<string> CreatePresignedPutUrlAsync(string objectKey, TimeSpan expiry,
        CancellationToken cancellationToken = default) =>
        Task.FromResult($"https://storage.test/{objectKey}?upload=1");

    public Task<string> CreatePresignedGetUrlAsync(string objectKey, TimeSpan expiry,
        CancellationToken cancellationToken = default) =>
        Task.FromResult($"https://storage.test/{objectKey}?signed=1");

    public Task<StoredObjectInfo?> GetInfoAsync(string objectKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<StoredObjectInfo?>(null);

    public Task<byte[]> ReadPrefixAsync(string objectKey, int length, CancellationToken cancellationToken = default) =>
        Task.FromResult(Array.Empty<byte>());

    public Task DownloadToFileAsync(
        string objectKey,
        string destinationPath,
        CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException("Test storage contains no video objects."));

    public Task UploadFileAsync(
        string objectKey,
        string sourcePath,
        string contentType,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
