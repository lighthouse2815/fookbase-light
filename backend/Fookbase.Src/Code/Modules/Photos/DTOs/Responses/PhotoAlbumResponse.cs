namespace Fookbase.Api.Modules.Photos.DTOs.Responses;

public sealed record PhotoAlbumResponse(
    Guid Id,
    Guid OwnerUserId,
    string Name,
    string? Description,
    string AlbumType,
    string Privacy,
    int PhotoCount,
    string? PreviewUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    bool CanManage);
