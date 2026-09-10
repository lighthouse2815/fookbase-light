namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record ChangePasswordRequest(
    string? CurrentPassword,
    string? NewPassword,
    string? ConfirmPassword);
