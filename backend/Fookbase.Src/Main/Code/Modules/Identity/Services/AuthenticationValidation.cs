using Fookbase.Api.Modules.Identity.DTOs.Requests;
using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Identity.Services;

internal static class AuthenticationValidation
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    public static IReadOnlyDictionary<string, string[]> Validate(RegisterRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        ValidateEmail(request.Email, errors);

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            errors["username"] = ["Username is required."];
        }
        else if (request.Username.Trim().Length is < 3 or > 32)
        {
            errors["username"] = ["Username must contain between 3 and 32 characters."];
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            errors["password"] = ["Password is required."];
        }
        else if (request.Password.Length is < 8 or > 128)
        {
            errors["password"] = ["Password must contain between 8 and 128 characters."];
        }

        return errors;
    }

    public static IReadOnlyDictionary<string, string[]> Validate(LoginRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (!ContactIdentifier.TryParse(request.EffectiveIdentifier, out _))
        {
            errors["identifier"] = ["Email or Vietnamese mobile number is invalid."];
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            errors["password"] = ["Password is required."];
        }

        return errors;
    }

    private static void ValidateEmail(
        string? email,
        IDictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            errors["email"] = ["Email is required."];
        }
        else if (!EmailValidator.IsValid(email.Trim()))
        {
            errors["email"] = ["Email is invalid."];
        }
    }
}
