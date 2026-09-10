namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record ResetPasswordRequest(
    string? Email,
    string? Token,
    string? Password,
    string? ConfirmPassword);
