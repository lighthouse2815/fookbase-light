namespace Fookbase.Api.Modules.Search.DTOs.Responses;

public sealed record SearchSuggestionsResponse(
    IReadOnlyList<SearchPersonResponse> People,
    IReadOnlyList<SearchGroupResponse> Groups,
    IReadOnlyList<SearchPageResponse> Pages);
