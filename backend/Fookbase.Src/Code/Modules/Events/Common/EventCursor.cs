using System.Globalization;
using System.Text;

namespace Fookbase.Api.Modules.Events.Common;

internal sealed record EventCursor(DateTimeOffset Timestamp, Guid Id)
{
    public static EventCursor? DecodeOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            if (value.Length > 256) throw new FormatException();
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(value)).Split('|');
            if (parts.Length != 2 ||
                !long.TryParse(parts[0], CultureInfo.InvariantCulture, out var ticks) ||
                !Guid.TryParse(parts[1], out var id) || id == Guid.Empty)
                throw new FormatException();
            return new EventCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("The event cursor is invalid.", exception);
        }
    }

    public string Encode() => Convert.ToBase64String(Encoding.UTF8.GetBytes(
        Timestamp.UtcTicks.ToString(CultureInfo.InvariantCulture) + "|" + Id));
}
