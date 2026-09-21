namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record TwoFactorCodeRequest(string? Code);
public sealed record TwoFactorVerifyRequest(string? Challenge, string? Code);
public sealed record DisableTwoFactorRequest(string? CurrentPassword);
