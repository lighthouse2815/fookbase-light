namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record ResetPasswordRequest(
    string? Email,
    string? Token,
    string? Password,
    string? ConfirmPassword)
{
    public string? Identifier { get; init; }
    public string? Code { get; init; }
    public string? FirebaseIdToken { get; init; }

    public string? EffectiveIdentifier => Identifier ?? Email;
}
