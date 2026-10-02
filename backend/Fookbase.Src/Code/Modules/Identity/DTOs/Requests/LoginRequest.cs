using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record LoginRequest(
    string? Email,
    [property: Required(ErrorMessage = "Password is required.")]
    string? Password)
{
    public string? Identifier { get; init; }

    public string? EffectiveIdentifier => Identifier ?? Email;
}
