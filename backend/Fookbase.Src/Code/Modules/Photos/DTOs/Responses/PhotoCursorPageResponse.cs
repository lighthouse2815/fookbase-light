namespace Fookbase.Api.Modules.Photos.DTOs.Responses;

public sealed record PhotoCursorPageResponse<T>(IReadOnlyList<T> Items, string? NextCursor);
