using Fookbase.Api.Modules.Search.Services;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Search.DTOs.Requests;

public sealed record SearchSuggestionsRequest(
    [FromQuery(Name = "q")] string? Query = null,
    int Limit = SearchService.DefaultSuggestionLimit);
