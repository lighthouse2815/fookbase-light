using System.Collections.Concurrent;
using Fookbase.Media.Application.Abstractions;

namespace Fookbase.Media.Api.IntegrationTests;

public sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, (byte[] Content, string ContentType)> objects = new();

    public Task<string> CreatePresignedPutUrlAsync(string objectKey, TimeSpan expiry,
        CancellationToken cancellationToken = default) => Task.FromResult($"https://storage.test/{objectKey}?put=1");

    public Task<string> CreatePresignedGetUrlAsync(string objectKey, TimeSpan expiry,
        CancellationToken cancellationToken = default) => Task.FromResult($"https://storage.test/{objectKey}?get=1");

    public Task<StoredObjectInfo?> GetInfoAsync(string objectKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(objects.TryGetValue(objectKey, out var value)
            ? new StoredObjectInfo(value.Content.LongLength, value.ContentType) : null);

    public Task<byte[]> ReadPrefixAsync(string objectKey, int length,
        CancellationToken cancellationToken = default)
    {
        var content = objects[objectKey].Content;
        if (length > content.Length)
        {
            throw new InvalidOperationException("A ranged read cannot extend beyond the stored object.");
        }

        return Task.FromResult(content.Take(length).ToArray());
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        objects.TryRemove(objectKey, out _);
        return Task.CompletedTask;
    }

    public void Put(string objectKey, byte[] content, string contentType) =>
        objects[objectKey] = (content, contentType);
}
