namespace Fookbase.Api.Modules.Identity.DTOs.Responses;

public sealed record TwoFactorRecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);
