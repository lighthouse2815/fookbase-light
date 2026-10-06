using System.Text.RegularExpressions;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Pages.Common;

public static partial class PageNormalization
{
    public static string NormalizeName(string? value)
    {
        var normalized = TextNormalization.NormalizeOptionalText(value) ?? string.Empty;
        if (normalized.Length is < 1 or > 120)
        {
            throw new ArgumentException("Page name must contain between 1 and 120 characters.");
        }

        return normalized;
    }

    public static string NormalizeUsername(string? username)
    {
        var normalized = TextNormalization.NormalizeOptionalText(username)?.ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is < 3 or > 50 ||
            !UsernamePattern().IsMatch(normalized))
        {
            throw new ArgumentException("Page username must contain 3-50 lowercase letters, digits, dots, or underscores.");
        }

        return normalized;
    }

    public static string NormalizeCategory(string? value)
    {
        var normalized = TextNormalization.NormalizeOptionalText(value) ?? string.Empty;
        if (normalized.Length is < 1 or > 80)
        {
            throw new ArgumentException("Page category must contain between 1 and 80 characters.");
        }

        return normalized;
    }

    public static string? NormalizeBio(string? value)
    {
        var normalized = TextNormalization.NormalizeOptionalText(value);
        if (normalized?.Length > 2_000)
        {
            throw new ArgumentException("Page bio cannot exceed 2000 characters.");
        }

        return normalized;
    }

    [GeneratedRegex("^[a-z0-9._]+$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();
}
