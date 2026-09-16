namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record RegistrationStartRequest(
    string? FirstName,
    string? LastName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Contact,
    string? Password);

public sealed record RegistrationVerifyRequest(Guid ChallengeId, string? Code)
{
    public string? FirebaseIdToken { get; init; }
}

public sealed record RegistrationResendRequest(Guid ChallengeId);
