namespace Fookbase.Api.Modules.Messages.DTOs.Responses;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Offset,
    int Limit,
    int Total,
    string? NextCursor = null);
