namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record LoginRequest(string? Email, string? Password)
{
    public string? Identifier { get; init; }

    public string? EffectiveIdentifier => Identifier ?? Email;
}
