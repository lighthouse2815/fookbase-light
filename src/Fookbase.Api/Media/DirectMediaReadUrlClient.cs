using Fookbase.Media.Application.Media;
using Fookbase.Posts.Application.Abstractions;

namespace Fookbase.Api.Media;

internal sealed class DirectMediaReadUrlClient(IMediaService mediaService) : IMediaReadUrlClient
{
    public async Task<MediaReadUrl?> CreateReadUrlAsync(
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var result = await mediaService.CreateReadUrlAsync(mediaId, cancellationToken);
        return result.Succeeded
            ? new MediaReadUrl(result.Value!.MediaId, result.Value.Url, result.Value.ExpiresAtUtc)
            : null;
    }
}
