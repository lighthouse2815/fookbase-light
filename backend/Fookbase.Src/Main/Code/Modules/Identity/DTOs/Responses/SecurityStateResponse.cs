namespace Fookbase.Api.Modules.Identity.DTOs.Responses;

public sealed record SecurityStateResponse(bool TwoFactorEnabled, int RecoveryCodesRemaining, int ActiveSessionCount);
