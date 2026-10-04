namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record GoogleMobileCompletionRequest(string? Code, string? Verifier, string? Password = null, string? Client = null);
