namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record LoginRequest(string? Email, string? Password);
