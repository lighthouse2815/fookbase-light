namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record RegisterRequest(string? Email, string? Username, string? Password);
