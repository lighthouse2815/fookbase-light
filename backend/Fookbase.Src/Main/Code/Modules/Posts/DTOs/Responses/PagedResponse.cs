namespace Fookbase.Api.Modules.Posts.DTOs.Responses;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Offset,
    int Limit,
    int Total);
