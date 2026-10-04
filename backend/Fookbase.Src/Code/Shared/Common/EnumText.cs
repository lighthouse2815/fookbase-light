using System.Globalization;

namespace Fookbase.Api.Shared.Common;

public static class EnumText
{
    public static string ToApiName(this Enum value) =>
        CultureInfo.InvariantCulture.TextInfo
            .ToTitleCase(value.ToString().ToLowerInvariant().Replace('_', ' '))
            .Replace(" ", string.Empty);

    public static bool TryParse<TEnum>(string? text, bool ignoreCase, out TEnum value)
        where TEnum : struct, Enum
    {
        if (Enum.TryParse(text, ignoreCase, out value))
        {
            return true;
        }

        if (text is null)
        {
            return false;
        }

        var names = text.Split(',');
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        for (var index = 0; index < names.Length; index++)
        {
            var name = names[index].Trim();
            var candidate = Enum.GetValues<TEnum>().Cast<Enum>().FirstOrDefault(item =>
                string.Equals(item.ToApiName(), name, comparison) ||
                string.Equals(item.ToString(), name, comparison));
            if (candidate is null)
            {
                return false;
            }

            names[index] = candidate.ToString();
        }

        return Enum.TryParse(string.Join(',', names), ignoreCase, out value);
    }
}
