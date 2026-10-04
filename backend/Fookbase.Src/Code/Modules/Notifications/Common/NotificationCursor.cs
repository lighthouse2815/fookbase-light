using System.Globalization;
using System.Text;

namespace Fookbase.Api.Modules.Notifications.Common;

internal sealed record NotificationCursor(DateTimeOffset CreatedAtUtc, Guid Id)
{
    public string Encode()
    {
        var payload = CreatedAtUtc.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture) + ":" + Id.ToString("N");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static NotificationCursor Decode(string value)
    {
        try
        {
            var encoded = value.Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight(encoded.Length + (4 - encoded.Length % 4) % 4, '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(encoded)).Split(':', 2);
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
