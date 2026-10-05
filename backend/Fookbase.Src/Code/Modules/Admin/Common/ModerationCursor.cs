using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Fookbase.Api.Modules.Admin.Common;

internal sealed record ModerationCursor(DateTimeOffset CreatedAtUtc, Guid Id)
{
    public static ModerationCursor? DecodeOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        try
        {
            var parts = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(value)).Split(':', 2);
            if (parts.Length != 2 ||
                !long.TryParse(parts[0], CultureInfo.InvariantCulture, out var ticks) ||
                !Guid.TryParseExact(parts[1], "N", out var id))
            {
                throw new FormatException("The moderation cursor is invalid.");
            }

            return new ModerationCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("The moderation cursor is invalid.", exception);
        }
    }

    public string Encode() => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(
        CreatedAtUtc.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture) + ":" + Id.ToString("N")));
}
