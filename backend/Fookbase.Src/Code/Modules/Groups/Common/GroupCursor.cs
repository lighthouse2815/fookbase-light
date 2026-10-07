using Microsoft.AspNetCore.WebUtilities;
using System.Globalization;
using System.Text;

namespace Fookbase.Api.Modules.Groups.Common;

internal sealed record GroupCursor(DateTimeOffset CreatedAtUtc, Guid Id)
{
    public static GroupCursor? DecodeOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Decode(value);

    public static GroupCursor Decode(string value)
    {
        try
        {
            var parts = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(value)).Split(':', 2);
            if (parts.Length != 2 ||
                !long.TryParse(parts[0], CultureInfo.InvariantCulture, out var ticks) ||
                !Guid.TryParseExact(parts[1], "N", out var id))
            {
                throw new FormatException("The group cursor is invalid.");
            }

            return new GroupCursor(new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc)), id);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("The group cursor is invalid.", exception);
        }
    }

    public string Encode()
    {
        var payload = CreatedAtUtc.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture) +
            ":" + Id.ToString("N");
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
    }
}
