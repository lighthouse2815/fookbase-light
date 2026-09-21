namespace Fookbase.Api.Modules.Friends.DTOs.Responses;

public sealed record MutualFriendsResponse(
    int Count,
    IReadOnlyList<Guid> UserIds,
    int Offset,
    int Limit);
