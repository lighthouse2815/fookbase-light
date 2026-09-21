namespace Fookbase.Api.Modules.Photos.DTOs.Responses;

public sealed record PhotoCursorPageResponse<T>(IReadOnlyList<T> Items, string? NextCursor);
public sealed record PhotoAlbumSummaryResponse(Guid Id, string Name, string AlbumType, string Privacy, int PhotoCount, string? PreviewUrl, DateTimeOffset CreatedAtUtc);
public sealed record PhotoAlbumResponse(Guid Id, Guid OwnerUserId, string Name, string? Description, string AlbumType, string Privacy, int PhotoCount, string? PreviewUrl, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, bool CanManage);
public sealed record AlbumMediaResponse(Guid MediaId, string? Caption, long SortOrder, DateTimeOffset AddedAtUtc, string AccessUrl);
public sealed record PhotoDetailResponse(Guid MediaId, Guid AlbumId, Guid OwnerUserId, string? Caption, DateTimeOffset AddedAtUtc, string Url);
