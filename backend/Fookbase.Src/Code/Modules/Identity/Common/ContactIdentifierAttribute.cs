using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Services;

namespace Fookbase.Api.Modules.Identity.Common;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ContactIdentifierAttribute : ValidationAttribute
{
    public ContactIdentifierAttribute()
        : base("Email hoặc số điện thoại không hợp lệ.")
    {
    }

    public override bool IsValid(object? value) =>
        value is null ||
        value is string identifier &&
        (string.IsNullOrWhiteSpace(identifier) || ContactIdentifier.TryParse(identifier, out _));
}
