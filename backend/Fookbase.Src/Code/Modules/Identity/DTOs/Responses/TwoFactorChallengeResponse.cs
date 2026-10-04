namespace Fookbase.Api.Modules.Identity.DTOs.Responses;

public sealed record TwoFactorChallengeResponse(bool TwoFactorRequired, string Challenge, DateTimeOffset ExpiresAtUtc);
