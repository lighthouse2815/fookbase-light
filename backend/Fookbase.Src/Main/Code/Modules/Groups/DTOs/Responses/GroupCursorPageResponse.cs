namespace Fookbase.Api.Modules.Groups.DTOs.Responses;

public sealed record GroupCursorPageResponse<T>(
    IReadOnlyList<T> Items,
    string? NextCursor);
