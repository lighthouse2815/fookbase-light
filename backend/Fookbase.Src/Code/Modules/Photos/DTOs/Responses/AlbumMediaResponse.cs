namespace Fookbase.Api.Modules.Photos.DTOs.Responses;

public sealed record AlbumMediaResponse(
    Guid MediaId,
    string? Caption,
    long SortOrder,
    DateTimeOffset AddedAtUtc,
    string AccessUrl);
