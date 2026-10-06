namespace Fookbase.Api.Modules.Events.DTOs.Responses;

public sealed record EventCursorPageResponse<T>(
    IReadOnlyList<T> Items,
    string? NextCursor);
