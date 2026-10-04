namespace Fookbase.Api.Modules.Pages.DTOs.Requests;

public sealed record CreatePageRequest(string Name, string Username, string Category, string? Bio);
