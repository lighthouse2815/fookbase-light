using System.Net.Http.Json;
using Fookbase.Posts.Application.Abstractions;

namespace Fookbase.Posts.Infrastructure.Media;

internal sealed class MediaReadUrlClient(HttpClient httpClient, MediaServiceOptions options) : IMediaReadUrlClient
{
    public async Task<MediaReadUrl?> CreateReadUrlAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/internal/media/{mediaId}/read-url");
        request.Headers.Add("X-Internal-Service-Token", options.InternalToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<MediaReadUrl>(cancellationToken);
    }
}
