namespace Fookbase.Api.Modules.Pages.DTOs.Responses;

public sealed record PageCursorPageResponse<T>(IReadOnlyList<T> Items, string? NextCursor);
