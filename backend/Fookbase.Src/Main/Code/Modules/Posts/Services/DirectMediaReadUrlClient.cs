using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Posts.Services;

namespace Fookbase.Api.Modules.Posts.Services;

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
