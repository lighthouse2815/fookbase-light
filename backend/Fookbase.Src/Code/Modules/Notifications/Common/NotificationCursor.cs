using Microsoft.AspNetCore.WebUtilities;
using System.Globalization;
using System.Text;

namespace Fookbase.Api.Modules.Notifications.Common;

internal sealed record NotificationCursor(DateTimeOffset CreatedAtUtc, Guid Id)
{
    public string Encode()
    {
        var payload = CreatedAtUtc.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture) + ":" + Id.ToString("N");
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
    }

    public static NotificationCursor Decode(string value)
    {
        try
        {
            var parts = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(value)).Split(':', 2);
            if (parts.Length != 2 ||
                !long.TryParse(parts[0], CultureInfo.InvariantCulture, out var ticks) ||
                !Guid.TryParseExact(parts[1], "N", out var id))
            {
                throw new FormatException("The notification cursor is invalid.");
            }

            return new NotificationCursor(new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc)), id);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("The notification cursor is invalid.", exception);
        }
    }
}
