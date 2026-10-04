namespace Fookbase.Api.Modules.Pages.DTOs.Requests;

public sealed record UpdatePageRequest(string Name, string Username, string Category, string? Bio);
