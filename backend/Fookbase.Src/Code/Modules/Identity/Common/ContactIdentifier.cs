using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Fookbase.Api.Modules.Identity.Domain.Enums;

namespace Fookbase.Api.Modules.Identity.Common;

public sealed record ContactIdentifier(ContactKind Kind, string Value)
{
    private static readonly EmailAddressAttribute EmailValidator = new();
    private static readonly Regex NonPhoneCharacters = new("[.\\s()\\-]", RegexOptions.Compiled);
    private static readonly Regex VietnameseMobile = new("^0(?:3|5|7|8|9)\\d{8}$", RegexOptions.Compiled);

    public static bool TryParse(string? raw, out ContactIdentifier contact)
    {
        contact = null!;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();
        if (EmailValidator.IsValid(trimmed))
        {
            contact = new ContactIdentifier(ContactKind.Email, trimmed.ToLowerInvariant());
            return true;
        }

        var digits = NonPhoneCharacters.Replace(trimmed, string.Empty);
        if (digits.StartsWith("+84", StringComparison.Ordinal))
        {
            digits = $"0{digits[3..]}";
        }
        else if (digits.StartsWith("84", StringComparison.Ordinal))
        {
            digits = $"0{digits[2..]}";
        }

        if (!VietnameseMobile.IsMatch(digits))
        {
            return false;
        }

        contact = new ContactIdentifier(ContactKind.Phone, $"+84{digits[1..]}");
        return true;
    }
}
