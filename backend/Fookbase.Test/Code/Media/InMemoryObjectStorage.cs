using Fookbase.Api.Modules.Media.Domain.Enums;
using Fookbase.Api.Modules.Media.Abstractions;
using Fookbase.Api.Modules.Media.DTOs.Responses;
using System.Collections.Concurrent;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Media.Services;

namespace Fookbase.Media.Api.IntegrationTests;

public sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, (byte[] Content, MediaType MediaType)> objects = new();

    public Task<DirectUploadIntent> CreateDirectUploadIntentAsync(
        string objectKey,
        MediaType mediaType,
        TimeSpan expiry,
        CancellationToken cancellationToken = default) => Task.FromResult(
        new DirectUploadIntent($"https://storage.test/{objectKey}?upload=1", new Dictionary<string, string>()));

    public Task<string> CreateSignedGetUrlAsync(string objectKey, MediaType mediaType,
        CancellationToken cancellationToken = default) => Task.FromResult($"https://storage.test/{objectKey}?get=1");

    public Task<StoredObjectInfo?> GetInfoAsync(string objectKey, MediaType mediaType,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(objects.TryGetValue(objectKey, out var value) && value.MediaType == mediaType
            ? new StoredObjectInfo(value.Content.LongLength, value.MediaType, true)
            : null);

    public Task<byte[]> ReadPrefixAsync(string objectKey, MediaType mediaType, int length,
        CancellationToken cancellationToken = default)
    {
        var content = objects[objectKey].Content;
        if (length > content.Length)
        {
            throw new InvalidOperationException("A ranged read cannot extend beyond the stored object.");
        }

        return Task.FromResult(content.Take(length).ToArray());
    }

    public Task DeleteAsync(string objectKey, MediaType mediaType, CancellationToken cancellationToken = default)
    {
        objects.TryRemove(objectKey, out _);
        return Task.CompletedTask;
    }

    public Task DownloadToFileAsync(
        string objectKey,
        MediaType mediaType,
        string destinationPath,
        CancellationToken cancellationToken = default) =>
        File.WriteAllBytesAsync(destinationPath, objects[objectKey].Content, cancellationToken);

    public async Task UploadFileAsync(
        string objectKey,
        MediaType mediaType,
        string sourcePath,
        string contentType,
        CancellationToken cancellationToken = default) =>
        Put(objectKey, await File.ReadAllBytesAsync(sourcePath, cancellationToken), mediaType);

    public void Put(string objectKey, byte[] content, string contentType) =>
        Put(objectKey, content, contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
            ? MediaType.VIDEO
            : MediaType.IMAGE);

    private void Put(string objectKey, byte[] content, MediaType mediaType) =>
        objects[objectKey] = (content, mediaType);

    public bool Contains(string objectKey) => objects.ContainsKey(objectKey);
}
