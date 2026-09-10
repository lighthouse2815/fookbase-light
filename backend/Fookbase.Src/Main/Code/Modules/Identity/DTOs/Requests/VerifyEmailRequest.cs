namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record VerifyEmailRequest(string? Email, string? Token);
