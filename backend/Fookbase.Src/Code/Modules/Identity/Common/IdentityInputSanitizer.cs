namespace Fookbase.Api.Modules.Identity.Common;

internal static class IdentityInputSanitizer
{
    public static string? SanitizeUserAgent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var sanitized = value.Trim()
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);

        return sanitized[..Math.Min(256, sanitized.Length)];
    }
}
