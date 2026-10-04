namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record TwoFactorVerifyRequest(string? Challenge, string? Code);
