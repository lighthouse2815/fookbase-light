namespace Fookbase.Api.Modules.Photos.DTOs.Responses;

public sealed record PhotoAlbumSummaryResponse(
    Guid Id,
    string Name,
    string AlbumType,
    string Privacy,
    int PhotoCount,
    string? PreviewUrl,
    DateTimeOffset CreatedAtUtc);
