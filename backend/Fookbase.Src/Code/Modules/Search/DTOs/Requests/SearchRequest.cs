using Fookbase.Api.Modules.Search.Services;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Search.DTOs.Requests;

public sealed record SearchRequest(
    [FromQuery(Name = "q")] string? Query = null,
    string? Type = null,
    string? Cursor = null,
    int Limit = SearchService.DefaultPageSize);
