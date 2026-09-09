namespace Fookbase.Api.Modules.Posts.Services;

public interface IMediaReadUrlClient
{
    Task<MediaReadUrl?> CreateReadUrlAsync(Guid mediaId, CancellationToken cancellationToken = default);
}

public sealed record MediaReadUrl(Guid MediaId, string Url, DateTimeOffset ExpiresAtUtc);
