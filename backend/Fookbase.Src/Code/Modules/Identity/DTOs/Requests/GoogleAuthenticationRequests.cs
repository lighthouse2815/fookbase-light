namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record GoogleCompletionRequest(string? Code, string? Client);

public sealed record GoogleLinkRequest(string? Code, string? Password, string? Client);

public sealed record GoogleMobileCompletionRequest(string? Code, string? Verifier, string? Password = null, string? Client = null);
