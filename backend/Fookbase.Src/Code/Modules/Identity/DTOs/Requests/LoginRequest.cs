using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record LoginRequest(
    string? Identifier,
    [property: Required(ErrorMessage = "Password is required.")]
    string? Password);
