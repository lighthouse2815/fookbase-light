namespace Fookbase.Api.Modules.Photos.DTOs.Responses;

public sealed record PhotoDetailResponse(
    Guid MediaId,
    Guid AlbumId,
    Guid OwnerUserId,
    string? Caption,
    DateTimeOffset AddedAtUtc,
    string Url);
