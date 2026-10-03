using System.Text.RegularExpressions;
using Fookbase.Api.Modules.Identity.Domain.Enums;

namespace Fookbase.Api.Modules.Identity.Common;

public sealed record ContactIdentifier(ContactKind Kind, string Value)
{
    private static readonly Regex NonPhoneCharacters = new("[.\\s()\\-]", RegexOptions.Compiled);

    // The request validates the identifier before it is classified and normalized here.
    public static ContactIdentifier Parse(string identifier)
    {
        var trimmed = identifier.Trim();
        if (trimmed.Contains('@'))
        {
            return new ContactIdentifier(ContactKind.Email, trimmed.ToLowerInvariant());
        }

        var digits = NonPhoneCharacters.Replace(trimmed, string.Empty);
        var phone = digits.StartsWith('0')
            ? $"+84{digits[1..]}"
            : $"+{digits.TrimStart('+')}";

        return new ContactIdentifier(ContactKind.Phone, phone);
    }
}
