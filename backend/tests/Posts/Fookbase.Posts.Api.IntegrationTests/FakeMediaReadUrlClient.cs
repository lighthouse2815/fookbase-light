using Fookbase.Posts.Application.Abstractions;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class FakeMediaReadUrlClient : IMediaReadUrlClient
{
    public Task<MediaReadUrl?> CreateReadUrlAsync(Guid mediaId, CancellationToken cancellationToken = default) =>
        Task.FromResult<MediaReadUrl?>(new(mediaId, $"https://storage.test/{mediaId}?signed=1",
            DateTimeOffset.UtcNow.AddMinutes(5)));
}
