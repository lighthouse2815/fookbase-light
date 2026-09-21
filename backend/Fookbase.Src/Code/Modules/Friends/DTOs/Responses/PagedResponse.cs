namespace Fookbase.Api.Modules.Friends.DTOs.Responses;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Offset,
    int Limit,
    int Total);
