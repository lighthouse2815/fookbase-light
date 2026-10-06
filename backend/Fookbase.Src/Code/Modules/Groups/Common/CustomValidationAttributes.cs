using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Groups.Common;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ValidGroupCursorAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null) return true;
        if (value is not string text) return false;

        try
        {
            _ = GroupCursor.DecodeOrNull(text);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
