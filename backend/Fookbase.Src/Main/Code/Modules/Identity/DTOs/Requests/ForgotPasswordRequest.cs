namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record ForgotPasswordRequest(string? Email)
{
    public string? Identifier { get; init; }

    public string? EffectiveIdentifier => Identifier ?? Email;
}
