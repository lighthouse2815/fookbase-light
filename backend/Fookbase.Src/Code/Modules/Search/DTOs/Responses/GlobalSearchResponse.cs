namespace Fookbase.Api.Modules.Search.DTOs.Responses;

public sealed record GlobalSearchResponse(
    IReadOnlyList<SearchPersonResponse> People,
    IReadOnlyList<SearchGroupResponse> Groups,
    IReadOnlyList<SearchPageResponse> Pages,
    IReadOnlyList<SearchPostResponse> Posts,
    IReadOnlyList<SearchReelResponse> Reels,
    string? NextCursor = null,
    IReadOnlyList<SearchHashtagResponse>? Hashtags = null,
    IReadOnlyList<SearchEventResponse>? Events = null);
