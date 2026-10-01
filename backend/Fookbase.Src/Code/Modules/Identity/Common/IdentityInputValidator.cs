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
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64 || !value.All(Uri.IsHexDigit))
        {
            throw new ArgumentException(
                $"{valueName} phải là chuỗi SHA-256 dạng thập lục phân.",
                parameterName);
        }
    }
}
