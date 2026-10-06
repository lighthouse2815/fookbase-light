using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Notifications.Common;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ValidNotificationCursorAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null || value is string text && string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (value is not string cursor)
        {
            return false;
        }

        try
        {
            _ = NotificationCursor.Decode(cursor);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
