namespace Fookbase.Api.Shared.Common;

public static class TextNormalization
{
    public static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
