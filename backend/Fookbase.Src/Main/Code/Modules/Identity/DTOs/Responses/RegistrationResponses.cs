namespace Fookbase.Api.Modules.Identity.DTOs.Responses;

public sealed record RegistrationChallengeResponse(
    Guid ChallengeId,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset ResendAvailableAtUtc,
    string VerificationMethod);
