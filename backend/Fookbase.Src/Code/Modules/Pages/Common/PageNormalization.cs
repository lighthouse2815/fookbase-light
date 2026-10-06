using System.Text.RegularExpressions;
using Fookbase.Api.Modules.Pages.Entities;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Pages.Common;

public static partial class PageNormalization
{
    public static string NormalizeName(string? value)
    {
        var normalized = TextNormalization.NormalizeOptionalText(value) ?? string.Empty;
        if (normalized.Length is < 1 or > Page.MaximumNameLength)
        {
            throw new ArgumentException($"Page name must contain between 1 and {Page.MaximumNameLength} characters.");
        }

        return normalized;
    }

    public static string NormalizeUsername(string? username)
    {
        var normalized = TextNormalization.NormalizeOptionalText(username)?.ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is < Page.MinimumUsernameLength or > Page.MaximumUsernameLength ||
            !UsernamePattern().IsMatch(normalized))
        {
            throw new ArgumentException($"Page username must contain {Page.MinimumUsernameLength}-{Page.MaximumUsernameLength} lowercase letters, digits, dots, or underscores.");
        }

        return normalized;
    }

    public static string NormalizeCategory(string? value)
    {
        var normalized = TextNormalization.NormalizeOptionalText(value) ?? string.Empty;
        if (normalized.Length is < 1 or > Page.MaximumCategoryLength)
        {
            throw new ArgumentException($"Page category must contain between 1 and {Page.MaximumCategoryLength} characters.");
        }

        return normalized;
    }

    public static string? NormalizeBio(string? value)
    {
        var normalized = TextNormalization.NormalizeOptionalText(value);
        if (normalized?.Length > Page.MaximumBioLength)
        {
            throw new ArgumentException($"Page bio cannot exceed {Page.MaximumBioLength} characters.");
        }

        return normalized;
    }

    [GeneratedRegex("^[a-z0-9._]+$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();
}
