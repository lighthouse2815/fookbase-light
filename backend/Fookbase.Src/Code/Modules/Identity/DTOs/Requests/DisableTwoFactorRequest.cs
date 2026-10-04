namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record DisableTwoFactorRequest(string? CurrentPassword);
