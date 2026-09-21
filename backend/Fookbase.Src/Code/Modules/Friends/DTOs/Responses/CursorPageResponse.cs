namespace Fookbase.Api.Modules.Friends.DTOs.Responses;

public sealed record CursorPageResponse<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    int Total);
