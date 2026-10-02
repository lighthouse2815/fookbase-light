namespace Fookbase.Api.Modules.Identity.Common;

internal static class IdentityInputValidator
{
    public static void ValidateText(string value, int maximumLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
        {
            throw new ArgumentException($"{name} không được để trống và có tối đa {maximumLength} ký tự.", name);
        }
    }

    public static void ValidateSha256Hex(string value, string valueName, string parameterName)
    {
        if (!IsValidSha256Hex(value))
        {
            throw new ArgumentException(
                $"{valueName} phải là chuỗi SHA-256 dạng thập lục phân.",
                parameterName);
        }
    }

    public static void ValidateOptionalExternalLogin(string? provider, string? providerKey)
    {
        if (string.IsNullOrWhiteSpace(provider) != string.IsNullOrWhiteSpace(providerKey))
        {
            throw new ArgumentException("A pending external provider and provider key must be supplied together.");
        }

        if (provider is { Length: > 32 } || providerKey is { Length: > 256 })
        {
            throw new ArgumentException("The pending external login is invalid.");
        }
    }

    public static bool IsValidSha256Hex(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length == 64 && value.All(Uri.IsHexDigit);
}
