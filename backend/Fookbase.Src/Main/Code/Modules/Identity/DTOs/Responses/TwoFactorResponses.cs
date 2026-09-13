namespace Fookbase.Api.Modules.Identity.DTOs.Responses;

public sealed record TwoFactorSetupResponse(string SharedKey, string OtpauthUri);
public sealed record TwoFactorRecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);
public sealed record TwoFactorChallengeResponse(bool TwoFactorRequired, string Challenge, DateTimeOffset ExpiresAtUtc);
