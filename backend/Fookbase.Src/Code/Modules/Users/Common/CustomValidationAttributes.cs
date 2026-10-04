using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Users.Common;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class OptionalEnumValueAttribute<TEnum> : ValidationAttribute where TEnum : struct, Enum
{
    public override bool IsValid(object? value) =>
        value is null ||
        value is string text &&
        (string.IsNullOrWhiteSpace(text) ||
         EnumText.TryParse(text, true, out TEnum parsed) && Enum.IsDefined(parsed));
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class OptionalHttpUrlAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is null ||
        value is string text &&
        (string.IsNullOrWhiteSpace(text) ||
         Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) &&
         (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
}
