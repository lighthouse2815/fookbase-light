namespace Fookbase.Api.Modules.Identity.DTOs.Responses;

public sealed record TwoFactorSetupResponse(string SharedKey, string OtpauthUri);
