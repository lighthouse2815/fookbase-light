using System.Collections.Concurrent;
using Fookbase.Media.Application.Abstractions;

namespace Fookbase.Media.Api.IntegrationTests;

public sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, byte[]> objects = new();

    public async Task PutAsync(
        string objectName,
        Stream content,
        long length,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await using var destination = new MemoryStream();
        await content.CopyToAsync(destination, cancellationToken);
        objects[objectName] = destination.ToArray();
    }

    public Task<Stream> OpenReadAsync(
        string objectName,
        CancellationToken cancellationToken = default)
    {
        if (!objects.TryGetValue(objectName, out var content))
        {
            throw new FileNotFoundException("The object was not found.", objectName);
        }

        return Task.FromResult<Stream>(new MemoryStream(content, writable: false));
    }

    public Task DeleteAsync(
        string objectName,
        CancellationToken cancellationToken = default)
    {
        objects.TryRemove(objectName, out _);
        return Task.CompletedTask;
    }
}
