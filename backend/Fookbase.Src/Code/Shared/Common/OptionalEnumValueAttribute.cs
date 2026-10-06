using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Shared.Common;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class OptionalEnumValueAttribute<TEnum> : ValidationAttribute where TEnum : struct, Enum
{
    public override bool IsValid(object? value) =>
        value is null ||
        value is string text &&
        (string.IsNullOrWhiteSpace(text) ||
         EnumText.TryParse(text, true, out TEnum parsed) && Enum.IsDefined(parsed));
}
