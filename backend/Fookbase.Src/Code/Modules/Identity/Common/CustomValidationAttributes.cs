using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Fookbase.Api.Modules.Identity.Common;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class EmailOrPhoneNumberAttribute : ValidationAttribute
{
    private static readonly EmailAddressAttribute EmailValidator = new();
    private static readonly Regex NonPhoneCharacters = new("[.\\s()\\-]", RegexOptions.Compiled);
    private static readonly Regex VietnameseMobile = new("^0(?:3|5|7|8|9)\\d{8}$", RegexOptions.Compiled);

    public EmailOrPhoneNumberAttribute()
        : base("Email hoặc số điện thoại không hợp lệ.")
    {
    }

    public override bool IsValid(object? value) =>
        value is null ||
        value is string identifier &&
        (string.IsNullOrWhiteSpace(identifier) || IsValidIdentifier(identifier));

    private static bool IsValidIdentifier(string identifier)
    {
        var trimmed = identifier.Trim();
        if (EmailValidator.IsValid(trimmed)) return true;

        var digits = NonPhoneCharacters.Replace(trimmed, string.Empty);
        if (digits.StartsWith("+84", StringComparison.Ordinal))
        {
            digits = $"0{digits[3..]}";
        }
        else if (digits.StartsWith("84", StringComparison.Ordinal))
        {
            digits = $"0{digits[2..]}";
        }

        return VietnameseMobile.IsMatch(digits);
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class TrimmedStringLengthAttribute(int maximumLength) : StringLengthAttribute(maximumLength)
{
    public override bool IsValid(object? value) =>
        base.IsValid(value is string text ? text.Trim() : value);
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NonEmptyGuidAttribute : ValidationAttribute
{
    public NonEmptyGuidAttribute() : base("Mã yêu cầu là bắt buộc.")
    {
    }

    public override bool IsValid(object? value) => value is Guid id && id != Guid.Empty;
}
